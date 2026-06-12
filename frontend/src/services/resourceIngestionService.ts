import { getStoredAuthSession } from "./authStorage";

const API_BASE = import.meta.env.VITE_API_URL?.trim();
if (!API_BASE) {
  throw new Error("Missing VITE_API_URL.");
}

export type AccessibleLearningResource = {
  id: string;
  title: string;
  description: string | null;
  topic: string;
  difficulty: number;
  estimatedDurationMinutes: number;
  contentType: string;
  sourceName?: string | null;
  url?: string | null;
  accessType?: string;
  accessInstructions?: string | null;
  visibility?: string;
  extractedTextSummary?: string | null;
  hasSupplementaryFile: boolean;
};

export type ResourceFileRecord = {
  id: string;
  learningResourceId: string;
  originalFileName: string;
  mimeType: string;
  sizeBytes: number;
  processingStatus: string;
  processingError: string | null;
  createdAtUtc: string;
  processedAtUtc: string | null;
  uploadedByUserId: string | null;
};

export type ResourceExtractedTextRecord = {
  learningResourceId: string;
  resourceFileId: string;
  summary: string | null;
  extractionMethod: string;
  characterCount: number;
  createdAtUtc: string;
};

function headersForLearner(learnerId: string): HeadersInit | undefined {
  const session = getStoredAuthSession();
  if (!session) return undefined;
  if (session.userId !== learnerId.trim()) return undefined;
  return { Authorization: `Bearer ${session.accessToken}` };
}

async function parseError(res: Response): Promise<never> {
  let message = `Request failed (${res.status})`;
  try {
    const body = (await res.json()) as { error?: string };
    if (body.error) message = body.error;
  } catch {
  }
  throw new Error(message);
}

export async function getAccessibleResources(learnerId: string): Promise<AccessibleLearningResource[]> {
  const res = await fetch(
    `${API_BASE}/users/${encodeURIComponent(learnerId)}/resources/accessible`,
    { headers: headersForLearner(learnerId) },
  );
  if (!res.ok) await parseError(res);
  return res.json();
}

export async function listResourceFiles(
  resourceId: string,
  learnerId: string,
): Promise<ResourceFileRecord[]> {
  const res = await fetch(
    `${API_BASE}/resources/${resourceId}/files?userId=${encodeURIComponent(learnerId)}`,
    { headers: headersForLearner(learnerId) },
  );
  if (!res.ok) await parseError(res);
  return res.json();
}

export async function getResourceExtractedText(
  resourceId: string,
  learnerId: string,
): Promise<ResourceExtractedTextRecord | null> {
  const res = await fetch(
    `${API_BASE}/resources/${resourceId}/extracted-text?userId=${encodeURIComponent(learnerId)}`,
    { headers: headersForLearner(learnerId) },
  );
  if (res.status === 404) return null;
  if (!res.ok) await parseError(res);
  return res.json();
}

export async function uploadResourceFile(
  resourceId: string,
  learnerId: string,
  file: File,
): Promise<ResourceFileRecord> {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(
    `${API_BASE}/resources/${resourceId}/files?userId=${encodeURIComponent(learnerId)}`,
    {
      method: "POST",
      body: form,
      headers: headersForLearner(learnerId),
    },
  );
  if (!res.ok) await parseError(res);
  return res.json();
}

export async function deleteResourceFile(
  resourceId: string,
  fileId: string,
  learnerId: string,
): Promise<void> {
  const res = await fetch(
    `${API_BASE}/resources/${resourceId}/files/${fileId}?userId=${encodeURIComponent(learnerId)}`,
    {
      method: "DELETE",
      headers: headersForLearner(learnerId),
    },
  );
  if (!res.ok) await parseError(res);
}
