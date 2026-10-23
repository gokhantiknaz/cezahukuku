/**
 * orval'ın ürettiği tüm istekler buradan geçer. İstekler aynı origin'deki /api yoluna gider;
 * Next.js rewrites bunları .NET API'ye iletir, böylece kimlik doğrulama cookie'si otomatik taşınır.
 */

export type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? problem?.title ?? `İstek başarısız (${status})`);
    this.name = "ApiError";
  }

  /** Doğrulama hatalarını tek listede döndürür (form altında göstermek için). */
  get messages(): string[] {
    const fieldErrors = Object.values(this.problem?.errors ?? {}).flat();
    return fieldErrors.length > 0 ? fieldErrors : [this.message];
  }
}

export async function apiFetch<T>(url: string, options: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    credentials: "same-origin",
    headers: { Accept: "application/json", ...options.headers },
  });

  const text = response.status === 204 ? "" : await response.text();
  const body = text ? JSON.parse(text) : undefined;

  if (!response.ok) {
    throw new ApiError(response.status, (body as ProblemDetails | undefined) ?? null);
  }

  return { data: body, status: response.status, headers: response.headers } as T;
}
