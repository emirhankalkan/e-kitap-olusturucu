export interface Paper {
  id: string;
  fileName: string;
  sortOrder: number;
  title: string | null;
  startPage: number | null;
}

export interface Book {
  id: string;
  name: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  papers: Paper[];
  createdAt: string;
  completedAt: string | null;
  errorMessage: string | null;
  pdfUrl: string | null;
  downloadUrl: string | null;
}

export interface SelectedFile { id: string; file: File }
