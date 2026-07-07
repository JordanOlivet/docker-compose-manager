<script lang="ts">
	import { onMount } from 'svelte';
	import { scaleLinear } from 'd3-scale';
	import { line as d3line, area as d3area, curveMonotoneX } from 'd3-shape';

	interface DataPoint {
		timestamp: Date;
		[key: string]: number | Date;
	}

	interface LineConfig {
		key: string;
		label: string;
		color: string;
	}

	interface Props {
		data: DataPoint[];
		lines: LineConfig[];
		height?: number;
		/** Visible time window in milliseconds (how much history is shown at once). */
		windowMs?: number;
		/**
		 * Render the right edge this many ms in the past. Keeping the sweep edge
		 * slightly behind wall-clock time means the next sample has already arrived
		 * before the edge reaches it, so the line is revealed continuously instead
		 * of snapping forward each poll. Set ~1x the polling interval.
		 */
		renderDelayMs?: number;
		formatValue?: (value: number) => string;
		/** Optional richer formatter used in the hover tooltip (falls back to formatValue). */
		formatTooltipValue?: (value: number) => string;
	}

	let {
		data,
		lines,
		height = 160,
		windowMs = 60_000,
		renderDelayMs = 1100,
		formatValue = (v) => v.toFixed(2),
		formatTooltipValue
	}: Props = $props();

	const tipValue = $derived(formatTooltipValue ?? formatValue);

	// Unique id so multiple charts on the same page don't share <defs>.
	const uid = Math.random().toString(36).slice(2, 9);

	const M = { top: 10, right: 12, bottom: 22, left: 46 };

	let containerEl: HTMLDivElement;
	let width = $state(600);
	let reducedMotion = false;

	// Plot geometry, recomputed each animation frame.
	interface SeriesGeo {
		key: string;
		color: string;
		linePath: string;
		areaPath: string;
		last: { x: number; y: number } | null;
	}
	let series = $state<SeriesGeo[]>([]);
	let yTicks = $state<{ y: number; label: string }[]>([]);
	let xTicks = $state<{ x: number; label: string }[]>([]);
	let hasData = $state(false);

	// Hover / tooltip state.
	interface HoverPoint {
		key: string;
		color: string;
		label: string;
		value: number;
		cy: number;
	}
	let hover = $state<{ x: number; time: Date; points: HoverPoint[] } | null>(null);
	let frozen = false; // pause auto-scroll while the pointer is over the chart

	// Eased Y domain to avoid vertical jitter on every frame.
	let easedMin: number | null = null;
	let easedMax: number | null = null;

	// `now` and effective delay used by the most recent render, so hover mapping
	// stays pixel-aligned with the (possibly frozen) geometry.
	let lastNow = Date.now();
	let lastEffDelay = 1100;

	const plotW = $derived(Math.max(0, width - M.left - M.right));
	const plotH = $derived(Math.max(0, height - M.top - M.bottom));

	function fmtTime(d: Date): string {
		return d.toLocaleTimeString('en-GB', {
			hour: '2-digit',
			minute: '2-digit',
			second: '2-digit'
		});
	}

	function numAt(d: DataPoint, key: string): number {
		const v = d[key];
		return typeof v === 'number' ? v : 0;
	}

	function computeGeo(nowMs: number) {
		const pw = plotW;
		const ph = plotH;
		if (pw <= 0 || ph <= 0) return;
		lastNow = nowMs;

		hasData = true;

		const empty = data.length === 0;

		// Sweep edge sits behind wall-clock so the next sample has already arrived
		// before the edge reaches it -> continuous reveal, no snap. The delay must
		// exceed the real inter-sample gap, which varies with backend latency
		// (slower for multi-service projects), so adapt it to the measured gap.
		let effDelay = renderDelayMs;
		if (!empty && data.length >= 2) {
			let maxGap = 0;
			for (let i = Math.max(1, data.length - 10); i < data.length; i++) {
				const g = data[i].timestamp.getTime() - data[i - 1].timestamp.getTime();
				if (g > maxGap) maxGap = g;
			}
			effDelay = Math.min(5000, Math.max(renderDelayMs, maxGap * 1.3 + 150));
		}
		lastEffDelay = effDelay;
		const edge = nowMs - effDelay;
		const minT = edge - windowMs;
		const xScale = scaleLinear().domain([minT, edge]).range([0, pw]);

		// Before any data arrives, render a flat zero baseline flowing across the
		// window instead of an empty placeholder, so the chart still looks live.
		let visible: DataPoint[];
		if (empty) {
			visible = [{ timestamp: new Date(minT) }, { timestamp: new Date(edge) }];
		} else {
			// Visible slice (+ one point just outside the left edge so the line reaches it).
			let startIdx = 0;
			for (let i = 0; i < data.length; i++) {
				if (data[i].timestamp.getTime() >= minT) {
					startIdx = Math.max(0, i - 1);
					break;
				}
				startIdx = i;
			}
			visible = data.slice(startIdx);
		}

		// Target Y domain from visible values.
		let dataMin = Infinity;
		let dataMax = -Infinity;
		for (const d of visible) {
			for (const l of lines) {
				const v = numAt(d, l.key);
				if (v < dataMin) dataMin = v;
				if (v > dataMax) dataMax = v;
			}
		}
		if (!isFinite(dataMin) || !isFinite(dataMax)) {
			dataMin = 0;
			dataMax = 1;
		}
		let targetMin: number;
		let targetMax: number;
		if (dataMin === dataMax) {
			if (dataMin === 0) {
				targetMin = 0;
				targetMax = 1;
			} else {
				targetMin = dataMin * 0.9;
				targetMax = dataMin * 1.1;
			}
		} else {
			const pad = (dataMax - dataMin) * 0.15;
			targetMin = dataMin - pad;
			targetMax = dataMax + pad;
			// Snap to zero baseline when values are non-negative and close to it.
			if (dataMin >= 0 && targetMin < (dataMax - dataMin) * 0.35) targetMin = 0;
		}

		// Ease the domain toward the target.
		const ease = frozen ? 1 : 0.18;
		easedMin = easedMin === null ? targetMin : easedMin + (targetMin - easedMin) * ease;
		easedMax = easedMax === null ? targetMax : easedMax + (targetMax - easedMax) * ease;
		const yScale = scaleLinear().domain([easedMin, easedMax]).range([ph, 0]);

		// Linear-interpolated series value at an arbitrary time (visible is ascending).
		function valueAt(t: number, key: string): number {
			for (let i = visible.length - 1; i >= 0; i--) {
				const ti = visible[i].timestamp.getTime();
				if (ti <= t) {
					if (i === visible.length - 1) return numAt(visible[i], key);
					const t1 = visible[i + 1].timestamp.getTime();
					const v0 = numAt(visible[i], key);
					const v1 = numAt(visible[i + 1], key);
					const f = t1 > ti ? (t - ti) / (t1 - ti) : 0;
					return v0 + (v1 - v0) * f;
				}
			}
			return numAt(visible[0], key);
		}

		// Leading dot rides the sweep edge (or the last real sample if data stalled).
		const lastTs = visible[visible.length - 1].timestamp.getTime();
		const dotT = Math.min(edge, lastTs);
		const dotX = xScale(dotT);

		// Build line + area generators per series.
		const nextSeries: SeriesGeo[] = lines.map((l) => {
			const lg = d3line<DataPoint>()
				.x((d) => xScale(d.timestamp.getTime()))
				.y((d) => yScale(numAt(d, l.key)))
				.curve(curveMonotoneX);
			const ag = d3area<DataPoint>()
				.x((d) => xScale(d.timestamp.getTime()))
				.y0(ph)
				.y1((d) => yScale(numAt(d, l.key)))
				.curve(curveMonotoneX);
			return {
				key: l.key,
				color: l.color,
				linePath: lg(visible) ?? '',
				areaPath: ag(visible) ?? '',
				last: !empty && dotX >= -1 && dotX <= pw + 1 ? { x: dotX, y: yScale(valueAt(dotT, l.key)) } : null
			};
		});
		series = nextSeries;

		// Y ticks.
		yTicks = yScale.ticks(4).map((t) => ({ y: yScale(t), label: formatValue(t) }));

		// X ticks (evenly spaced across the window).
		const tickCount = 4;
		const xt: { x: number; label: string }[] = [];
		for (let i = 0; i <= tickCount; i++) {
			const t = minT + (windowMs * i) / tickCount;
			xt.push({ x: xScale(t), label: fmtTime(new Date(t)) });
		}
		xTicks = xt;

		// Recompute hover readout if frozen (data may still update underneath).
		if (frozen && hover) updateHoverAt(hover.x, nowMs);
	}

	function updateHoverAt(px: number, nowMs: number) {
		if (data.length === 0) {
			hover = null;
			return;
		}
		const edge = nowMs - lastEffDelay;
		const minT = edge - windowMs;
		const xScale = scaleLinear().domain([minT, edge]).range([0, plotW]);
		const yScale = scaleLinear()
			.domain([easedMin ?? 0, easedMax ?? 1])
			.range([plotH, 0]);
		const targetT = xScale.invert(Math.max(0, Math.min(plotW, px)));

		// Nearest data point by timestamp.
		let best = data[0];
		let bestDist = Infinity;
		for (const d of data) {
			const dist = Math.abs(d.timestamp.getTime() - targetT);
			if (dist < bestDist) {
				bestDist = dist;
				best = d;
			}
		}
		const snapX = xScale(best.timestamp.getTime());
		hover = {
			x: snapX,
			time: best.timestamp,
			points: lines.map((l) => {
				const value = numAt(best, l.key);
				return { key: l.key, color: l.color, label: l.label, value, cy: yScale(value) };
			})
		};
	}

	function onPointerMove(e: PointerEvent) {
		const rect = containerEl.getBoundingClientRect();
		const px = e.clientX - rect.left - M.left;
		if (px < 0 || px > plotW) {
			hover = null;
			return;
		}
		frozen = true;
		updateHoverAt(px, lastNow);
	}

	function onPointerLeave() {
		frozen = false;
		hover = null;
	}

	onMount(() => {
		reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

		const ro = new ResizeObserver((entries) => {
			for (const entry of entries) width = entry.contentRect.width;
		});
		ro.observe(containerEl);
		width = containerEl.getBoundingClientRect().width;

		let raf = 0;
		const loop = () => {
			if (!frozen) computeGeo(Date.now());
			raf = requestAnimationFrame(loop);
		};

		if (reducedMotion) {
			// Redraw on data changes only.
			computeGeo(Date.now());
			const id = setInterval(() => {
				if (!frozen) computeGeo(Date.now());
			}, 1000);
			return () => {
				clearInterval(id);
				ro.disconnect();
			};
		}

		raf = requestAnimationFrame(loop);
		return () => {
			cancelAnimationFrame(raf);
			ro.disconnect();
		};
	});

	// Tooltip horizontal placement (flip near the right edge).
	const tipLeft = $derived.by(() => {
		if (!hover) return 0;
		const abs = M.left + hover.x;
		return abs > width - 150 ? abs - 150 : abs + 12;
	});
</script>

<div
	bind:this={containerEl}
	class="relative w-full select-none"
	style="height: {height}px;"
	role="img"
	aria-label="Live resource chart"
	onpointermove={onPointerMove}
	onpointerleave={onPointerLeave}
>
	{#if hasData}
		<svg class="block h-full w-full overflow-visible" {width} {height}>
			<defs>
				<clipPath id="clip-{uid}">
					<rect x="0" y="0" width={plotW} height={plotH} />
				</clipPath>
				{#each lines as l (l.key)}
					<linearGradient id="grad-{uid}-{l.key}" x1="0" y1="0" x2="0" y2="1">
						<stop offset="0%" stop-color={l.color} stop-opacity="0.28" />
						<stop offset="100%" stop-color={l.color} stop-opacity="0" />
					</linearGradient>
				{/each}
			</defs>

			<g transform="translate({M.left},{M.top})">
				<!-- Grid + Y labels -->
				{#each yTicks as tick, i (i)}
					<line
						x1="0"
						x2={plotW}
						y1={tick.y}
						y2={tick.y}
						class="stroke-gray-200 dark:stroke-gray-700/70"
						stroke-width="1"
					/>
					<text
						x="-8"
						y={tick.y}
						text-anchor="end"
						dominant-baseline="middle"
						class="fill-gray-400 dark:fill-gray-500"
						style="font-size:10px;"
					>
						{tick.label}
					</text>
				{/each}

				<!-- X labels -->
				{#each xTicks as tick, i (i)}
					<text
						x={tick.x}
						y={plotH + 15}
						text-anchor="middle"
						class="fill-gray-400 dark:fill-gray-500"
						style="font-size:10px;"
					>
						{tick.label}
					</text>
				{/each}

				<!-- Series (clipped to plot area) -->
				<g clip-path="url(#clip-{uid})">
					{#each series as s (s.key)}
						<path d={s.areaPath} fill="url(#grad-{uid}-{s.key})" stroke="none" />
						<path
							d={s.linePath}
							fill="none"
							stroke={s.color}
							stroke-width="2"
							stroke-linejoin="round"
							stroke-linecap="round"
						/>
					{/each}

					<!-- Leading dot on the newest sample -->
					{#if !hover}
						{#each series as s (s.key)}
							{#if s.last}
								<circle cx={s.last.x} cy={s.last.y} r="3" fill={s.color} />
								<circle cx={s.last.x} cy={s.last.y} r="3" fill={s.color} opacity="0.35">
									<animate attributeName="r" from="3" to="8" dur="1.4s" repeatCount="indefinite" />
									<animate
										attributeName="opacity"
										from="0.35"
										to="0"
										dur="1.4s"
										repeatCount="indefinite"
									/>
								</circle>
							{/if}
						{/each}
					{/if}
				</g>

				<!-- Hover crosshair + focus dots -->
				{#if hover}
					<line
						x1={hover.x}
						x2={hover.x}
						y1="0"
						y2={plotH}
						class="stroke-gray-400 dark:stroke-gray-500"
						stroke-width="1"
						stroke-dasharray="3,3"
					/>
					{#each hover.points as p (p.key)}
						<circle
							cx={hover.x}
							cy={p.cy}
							r="4"
							fill={p.color}
							class="stroke-white dark:stroke-gray-800"
							stroke-width="2"
						/>
					{/each}
				{/if}
			</g>
		</svg>

		<!-- Tooltip -->
		{#if hover}
			<div
				class="pointer-events-none absolute top-1 z-20 rounded-lg border border-gray-200 bg-white/95 px-2.5 py-1.5 text-xs shadow-lg backdrop-blur-sm dark:border-gray-700 dark:bg-gray-800/95"
				style="left:{tipLeft}px;"
			>
				<div class="mb-1 font-mono text-[10px] text-gray-400 dark:text-gray-500">
					{fmtTime(hover.time)}
				</div>
				{#each hover.points as p (p.key)}
					<div class="flex items-center gap-1.5 whitespace-nowrap">
						<span class="h-2 w-2 rounded-full" style="background-color:{p.color};"></span>
						<span class="text-gray-500 dark:text-gray-400">{p.label}</span>
						<span class="ml-auto font-mono font-semibold text-gray-900 dark:text-white">
							{tipValue(p.value)}
						</span>
					</div>
				{/each}
			</div>
		{/if}
	{:else}
		<div class="flex h-full items-center justify-center text-sm text-gray-400">
			Waiting for data…
		</div>
	{/if}
</div>
