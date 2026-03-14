<script lang="ts">
  import { GripVertical } from 'lucide-svelte';
  import type { ColumnDefinition } from '$lib/types/table';
  import { t } from '$lib/i18n';

  interface Props {
    /** Column definitions */
    columns: ColumnDefinition[];
    /** Current column order (array of column IDs) */
    columnOrder: string[];
    /** Current sort key */
    sortKey?: string;
    /** Current sort direction */
    sortDir?: 'asc' | 'desc';
    /** Callback when a sortable column header is clicked */
    onSort?: (key: string) => void;
    /** Callback when columns are reordered */
    onReorder: (fromIndex: number, toIndex: number) => void;
    /** Additional header row classes */
    class?: string;
  }

  let {
    columns,
    columnOrder,
    sortKey,
    sortDir,
    onSort,
    onReorder,
    class: className = ''
  }: Props = $props();

  // Drag state
  let draggedIndex = $state<number | null>(null);
  let dragOverIndex = $state<number | null>(null);

  // Get columns in order
  const orderedColumns = $derived.by(() => {
    const columnMap = new Map(columns.map(c => [c.id, c]));
    return columnOrder
      .map(id => columnMap.get(id))
      .filter((c): c is ColumnDefinition => c !== undefined);
  });

  function handleDragStart(e: DragEvent, index: number) {
    const column = orderedColumns[index];
    if (column?.fixed) {
      e.preventDefault();
      return;
    }

    draggedIndex = index;

    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
      e.dataTransfer.setData('text/plain', index.toString());
    }
  }

  function handleDragOver(e: DragEvent, index: number) {
    e.preventDefault();

    const column = orderedColumns[index];
    if (column?.fixed) {
      e.dataTransfer!.dropEffect = 'none';
      return;
    }

    e.dataTransfer!.dropEffect = 'move';
    dragOverIndex = index;
  }

  function handleDragLeave() {
    dragOverIndex = null;
  }

  function handleDrop(e: DragEvent, toIndex: number) {
    e.preventDefault();

    const column = orderedColumns[toIndex];
    if (column?.fixed) return;

    if (draggedIndex !== null && draggedIndex !== toIndex) {
      onReorder(draggedIndex, toIndex);
    }

    draggedIndex = null;
    dragOverIndex = null;
  }

  function handleDragEnd() {
    draggedIndex = null;
    dragOverIndex = null;
  }

  function handleHeaderClick(column: ColumnDefinition) {
    if (column.sortKey && onSort) {
      onSort(column.sortKey);
    }
  }
</script>

<thead class="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700 {className}">
  <tr>
    {#each orderedColumns as column, index (column.id)}
      {@const isDragging = draggedIndex === index}
      {@const isDragOver = dragOverIndex === index && draggedIndex !== index}
      {@const isSortable = !!column.sortKey}
      {@const isSorted = column.sortKey === sortKey}
      {@const isDraggable = !column.fixed}

      <th
        draggable={isDraggable}
        ondragstart={(e) => handleDragStart(e, index)}
        ondragover={(e) => handleDragOver(e, index)}
        ondragleave={handleDragLeave}
        ondrop={(e) => handleDrop(e, index)}
        ondragend={handleDragEnd}
        onclick={() => handleHeaderClick(column)}
        class="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider select-none transition-all duration-150
          {isSortable ? 'cursor-pointer' : ''}
          {isDraggable ? 'cursor-grab active:cursor-grabbing' : ''}
          {isDragging ? 'opacity-50 bg-gray-200 dark:bg-gray-700' : ''}
          {isDragOver ? 'bg-blue-100 dark:bg-blue-900/30 border-l-2 border-blue-500' : ''}"
        style={column.width ? `width: ${column.width}` : undefined}
      >
        <div class="flex items-center gap-1">
          {#if isDraggable}
            <GripVertical class="w-3 h-3 text-gray-400 dark:text-gray-500 shrink-0 opacity-50 hover:opacity-100" />
          {/if}
          <span class="truncate">{$t(column.labelKey)}</span>
          {#if isSorted}
            <span class="inline-block ml-1 shrink-0">
              {sortDir === 'asc' ? '↑' : '↓'}
            </span>
          {/if}
        </div>
      </th>
    {/each}
  </tr>
</thead>
