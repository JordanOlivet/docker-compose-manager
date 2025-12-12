<script lang="ts">
  import { LayerCake, Svg, Html } from 'layercake';
  import { scaleBand, scaleLinear } from 'd3-scale';

  interface DataPoint {
    label: string;
    value: number;
  }

  interface Props {
    data: DataPoint[];
    height?: number;
    color?: string;
  }

  let { data, height = 200, color = '#3b82f6' }: Props = $props();
</script>

<div style="height: {height}px;">
  {#if data.length > 0}
    <LayerCake
      padding={{ top: 10, right: 10, bottom: 20, left: 40 }}
      x="label"
      y="value"
      xScale={scaleBand().paddingInner(0.1)}
      yScale={scaleLinear()}
      {data}
    >
      <Svg>
        <!-- Y-axis -->
        <g class="axis y-axis" transform="translate(0, 0)">
          {#each [0, 25, 50, 75, 100] as tick}
            <g transform="translate(0, {(1 - tick / 100) * (height - 30)})">
              <line x1="0" x2="100%" stroke="#e5e7eb" stroke-width="1" />
              <text x="-5" y="4" text-anchor="end" class="fill-gray-400 text-xs">{tick}%</text>
            </g>
          {/each}
        </g>

        <!-- Bars -->
        {#each data as d, i}
          {@const barWidth = 100 / data.length * 0.8}
          {@const barX = (100 / data.length) * i + (100 / data.length) * 0.1}
          {@const barHeight = (d.value / 100) * (height - 30)}
          <rect
            x="{barX}%"
            y={height - 30 - barHeight}
            width="{barWidth}%"
            height={barHeight}
            fill={color}
            rx="4"
            class="transition-all duration-300"
          />
        {/each}
      </Svg>

      <!-- Labels -->
      <Html>
        <div class="absolute bottom-0 left-0 right-0 flex justify-around text-xs text-gray-500">
          {#each data as d}
            <span class="truncate px-1">{d.label}</span>
          {/each}
        </div>
      </Html>
    </LayerCake>
  {:else}
    <div class="flex items-center justify-center h-full text-gray-400">
      No data available
    </div>
  {/if}
</div>
