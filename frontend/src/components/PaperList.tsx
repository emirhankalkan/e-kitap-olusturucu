import {
  DndContext, KeyboardSensor, PointerSensor, closestCenter, useSensor, useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext, arrayMove, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { FileText, GripVertical, X } from 'lucide-react';
import type { SelectedFile } from '../types/book';

interface Props {
  files: SelectedFile[];
  disabled: boolean;
  onRemove: (id: string) => void;
  onReorder: (files: SelectedFile[]) => void;
}

export default function PaperList({ files, disabled, onRemove, onReorder }: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  if (!files.length) return null;

  function handleDragEnd({ active, over }: DragEndEvent) {
    if (!over || active.id === over.id) return;
    const oldIndex = files.findIndex(item => item.id === active.id);
    const newIndex = files.findIndex(item => item.id === over.id);
    if (oldIndex !== -1 && newIndex !== -1) onReorder(arrayMove(files, oldIndex, newIndex));
  }

  return <div className="paper-list">
    <div className="list-caption"><span>Kitaptaki sıralama</span><span>Tutamaçtan sürükleyin</span></div>
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext items={files.map(item => item.id)} strategy={verticalListSortingStrategy}>
        <ol>{files.map((item, index) => <SortablePaper key={item.id} item={item} index={index} disabled={disabled} onRemove={onRemove} />)}</ol>
      </SortableContext>
    </DndContext>
  </div>;
}

function SortablePaper({ item, index, disabled, onRemove }: {
  item: SelectedFile;
  index: number;
  disabled: boolean;
  onRemove: (id: string) => void;
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({ id: item.id, disabled });
  const style = { transform: CSS.Transform.toString(transform), transition };

  return <li ref={setNodeRef} style={style} className={isDragging ? 'is-sorting' : ''}>
    <button ref={setActivatorNodeRef} type="button" className="drag-handle" aria-label={`${item.file.name} dosyasını sırala`} disabled={disabled} {...attributes} {...listeners}><GripVertical size={18} /></button>
    <span className="paper-number">{String(index + 1).padStart(2, '0')}</span>
    <FileText className="paper-icon" size={19} />
    <div className="min-w-0 flex-1"><p className="file-name" title={item.file.name}>{item.file.name}</p><span className="file-size">{Math.max(1, Math.round(item.file.size / 1024))} KB · Word belgesi</span></div>
    <button type="button" className="icon-button remove" aria-label={`${item.file.name} kaldır`} disabled={disabled} onClick={() => onRemove(item.id)}><X size={16} /></button>
  </li>;
}
