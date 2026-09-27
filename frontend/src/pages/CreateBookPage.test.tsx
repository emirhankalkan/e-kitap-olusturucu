import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CreateBookPage from './CreateBookPage';
import { generateBook, getBook, uploadBook } from '../services/booksApi';
import type { Book } from '../types/book';

vi.mock('../services/booksApi', () => ({
  uploadBook: vi.fn(),
  generateBook: vi.fn(),
  getBook: vi.fn(),
}));

const pendingBook: Book = {
  id: '65b11976-d833-4e2c-af91-cb74cd1abfb2',
  name: 'Bilimsel Çalışmalar',
  status: 'Pending',
  createdAt: '2026-09-27T12:00:00Z',
  completedAt: null,
  errorMessage: null,
  pdfUrl: null,
  downloadUrl: null,
  papers: Array.from({ length: 10 }, (_, index) => ({
    id: `paper-${index + 1}`,
    fileName: `${String(index + 1).padStart(2, '0')}.docx`,
    sortOrder: index + 1,
    title: null,
    startPage: null,
  })),
};

const completedBook: Book = {
  ...pendingBook,
  status: 'Completed',
  completedAt: '2026-09-27T12:01:00Z',
  pdfUrl: `/api/books/${pendingBook.id}/pdf`,
  downloadUrl: `/api/books/${pendingBook.id}/download`,
  papers: pendingBook.papers.map((paper, index) => ({
    ...paper,
    title: `Bildiri ${index + 1}`,
    startPage: index + 2,
  })),
};

describe('CreateBookPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('points out the missing book name and file count', async () => {
    const user = userEvent.setup();
    render(<CreateBookPage />);

    await user.click(screen.getByRole('button', { name: 'Kitabı oluştur' }));

    expect(screen.getAllByText('Devam etmek için kitap adını girin.').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Tam olarak 10 Word dosyası yükleyin/).length).toBeGreaterThan(0);
    expect(screen.getByLabelText('Kitap adı *')).toHaveFocus();
    expect(uploadBook).not.toHaveBeenCalled();
  });

  it('uploads ten files in the visible order and shows the completed book', async () => {
    vi.mocked(uploadBook).mockResolvedValue(pendingBook);
    vi.mocked(generateBook).mockResolvedValue(completedBook);
    const user = userEvent.setup();
    render(<CreateBookPage />);

    await user.type(screen.getByLabelText('Kitap adı *'), pendingBook.name);
    const files = Array.from({ length: 10 }, (_, index) =>
      new File(['docx'], `${String(index + 1).padStart(2, '0')}.docx`, {
        type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      }));
    await user.upload(screen.getByLabelText('Word bildirilerini seç'), files);

    expect(screen.getByText('10 / 10 dosya')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Kitabı oluştur' }));

    expect(await screen.findByText('KİTABINIZ HAZIR')).toBeInTheDocument();
    expect(uploadBook).toHaveBeenCalledWith(pendingBook.name, files);
    expect(generateBook).toHaveBeenCalledWith(pendingBook.id);
    expect(screen.getByTitle('Oluşturulan e-kitabın PDF önizlemesi')).toHaveAttribute('src', completedBook.pdfUrl);
    expect(screen.getByRole('link', { name: 'PDF’i indir' })).toHaveAttribute('href', completedBook.downloadUrl);
  });

  it('shows a clear generation error and keeps the failed book retryable', async () => {
    vi.mocked(uploadBook).mockResolvedValue(pendingBook);
    vi.mocked(generateBook).mockRejectedValue(new Error('Kitap oluşturulamadı. Belgeleri kontrol edip yeniden deneyin.'));
    vi.mocked(getBook).mockResolvedValue({
      ...pendingBook,
      status: 'Failed',
      errorMessage: 'Kitap oluşturulamadı. Belgeleri kontrol edip yeniden deneyin.',
    });
    const user = userEvent.setup();
    render(<CreateBookPage />);

    await user.type(screen.getByLabelText('Kitap adı *'), pendingBook.name);
    await user.upload(screen.getByLabelText('Word bildirilerini seç'),
      Array.from({ length: 10 }, (_, index) => new File(['docx'], `${index + 1}.docx`)));
    await user.click(screen.getByRole('button', { name: 'Kitabı oluştur' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Kitap oluşturulamadı');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Yeniden dene' })).toBeEnabled());
    expect(getBook).toHaveBeenCalledWith(pendingBook.id);
  });
});
