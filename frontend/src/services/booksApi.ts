import type { Book } from '../types/book';

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try { response = await fetch(url, init); }
  catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new Error('Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin.');
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as {
      title?: string; detail?: string; errors?: Record<string, string[]>;
    } | null;
    const details = problem?.errors && Object.values(problem.errors).flat().join(' ');
    throw new Error(details || problem?.detail || problem?.title || 'İşlem tamamlanamadı. Yeniden deneyin.');
  }
  return response.json() as Promise<T>;
}

export function uploadBook(name: string, files: File[]) {
  const form = new FormData();
  form.append('Name', name.trim());
  files.forEach(file => form.append('Files', file));
  return request<Book>('/api/books', { method: 'POST', body: form });
}

export const getBook = (id: string, signal?: AbortSignal) =>
  request<Book>(`/api/books/${encodeURIComponent(id)}`, { signal });
export const generateBook = (id: string) =>
  request<Book>(`/api/books/${encodeURIComponent(id)}/generate`, { method: 'POST' });
