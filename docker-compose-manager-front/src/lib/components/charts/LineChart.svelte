<script lang="ts">
	import { scaleLinear, scaleTime } from 'd3-scale';

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
		yAxisLabel?: string;
		formatValue?: (value: number) => string;
	}

	let { data, lines, height = 200, yAxisLabel = '', formatValue = (v) => v.toFixed(2) }: Props =
		$props();

	// Calculate min/max for Y axis with auto-scaling
	const yExtent = $derived.by(() => {
		if (data.length === 0) return [0, 100];

		const allValues = data.flatMap((d) =>
			lines.map((line) => (typeof d[line.key] === 'number' ? (d[line.key] as number) : 0))
		);

		if (allValues.length === 0) return [0, 100];

		const dataMin = Math.min(...allValues);
		const dataMax = Math.max(...allValues);

		// If all values are the same, create a small range around that value
		if (dataMin === dataMax) {
			if (dataMin === 0) return [0, 10];
			return [dataMin * 0.9, dataMin * 1.1];
		}

		const range = dataMax - dataMin;
		const padding = range * 0.1; // 10% padding

		// Calculate min with padding
		let min = dataMin - padding;
		// If min is very close to 0 (within 5% of range from 0), snap to 0 for cleaner axis
		if (min < 0 && dataMin >= 0) min = 0;
		if (Math.abs(min) < range * 0.05) min = 0;

		// Calculate max with padding
		const max = dataMax + padding;

		return [min, max];
	});

	const width = $derived(800); // Fixed width for now
	const chartHeight = $derived(height - 60); // Account for padding

	// Create scales
	const xScale = $derived.by(() => {
		if (data.length === 0) return scaleTime().domain([new Date(), new Date()]).range([0, width]);
		const timestamps = data.map((d) => d.timestamp);
		return scaleTime()
			.domain([Math.min(...timestamps.map((t) => t.getTime())), Math.max(...timestamps.map((t) => t.getTime()))])
			.range([0, width]);
	});

	const yScale = $derived.by(() => {
		return scaleLinear().domain(yExtent).range([chartHeight, 0]);
	});

	// Generate path for a line
	function generatePath(data: DataPoint[], key: string): string {
		if (data.length === 0) return '';

		const points = data.map((d, i) => {
			const x = (i / (data.length - 1 || 1)) * width;
			const value = typeof d[key] === 'number' ? (d[key] as number) : 0;
			const y = yScale(value);
			return `${x},${y}`;
		});

		return `M ${points.join(' L ')}`;
	}

	// Format time for X axis
	function formatTime(date: Date): string {
		return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
	}

	// Calculate Y axis ticks
	const yTicks = $derived.by(() => {
		const tickCount = 5;
		const step = (yExtent[1] - yExtent[0]) / (tickCount - 1);
		return Array.from({ length: tickCount }, (_, i) => yExtent[0] + step * i);
	});
</script>

<div style="height: {height}px;" class="relative w-full overflow-hidden">
	{#if data.length > 0}
		<!-- Legend -->
		<div class="flex justify-center items-center gap-4 mb-2 pt-1">
			{#each lines as line}
				<div class="flex items-center gap-1.5 text-xs">
					<div class="w-3 h-3 rounded-full" style="background-color: {line.color}"></div>
					<span class="text-gray-700 dark:text-gray-300 font-medium">{line.label}</span>
				</div>
			{/each}
		</div>

		<!-- SVG Chart -->
		<svg class="w-full" style="height: {height - 30}px;" viewBox="0 0 {width + 80} {height - 30}">
			<g transform="translate(60, 10)">
				<!-- Grid lines -->
				<g class="grid">
					{#each yTicks as tick}
						{@const y = yScale(tick)}
						<line
							x1="0"
							x2={width}
							y1={y}
							y2={y}
							stroke="#e5e7eb"
							class="dark:stroke-gray-700"
							stroke-width="1"
						/>
					{/each}
				</g>

				<!-- Y-axis labels -->
				<g class="y-axis">
					{#each yTicks as tick}
						{@const y = yScale(tick)}
						<text
							x="-10"
							y={y}
							text-anchor="end"
							dominant-baseline="middle"
							class="fill-gray-600 dark:fill-gray-400 text-xs"
						>
							{formatValue(tick)}
						</text>
					{/each}
				</g>

				<!-- Lines -->
				{#each lines as line}
					<path
						d={generatePath(data, line.key)}
						fill="none"
						stroke={line.color}
						stroke-width="2"
						class="transition-all duration-300"
					/>
				{/each}

				<!-- X-axis labels -->
				{#if data.length > 0}
					{@const firstPoint = data[0]}
					{@const lastPoint = data[data.length - 1]}
					<text
						x="0"
						y={chartHeight + 20}
						text-anchor="start"
						class="fill-gray-600 dark:fill-gray-400 text-xs"
					>
						{formatTime(firstPoint.timestamp)}
					</text>
					<text
						x={width}
						y={chartHeight + 20}
						text-anchor="end"
						class="fill-gray-600 dark:fill-gray-400 text-xs"
					>
						{formatTime(lastPoint.timestamp)}
					</text>
				{/if}
			</g>
		</svg>
	{:else}
		<div class="flex items-center justify-center h-full text-gray-400">No data available</div>
	{/if}
</div>
