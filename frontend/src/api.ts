export type User = { id:string; nome:string; email:string; papeis:string[]; ehAdministrador:boolean };
export type Operation = { sucesso:boolean; erros:string[] };

export async function api<T = unknown>(path:string, init:RequestInit = {}):Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type','application/json');
  const response = await fetch(path, { ...init, headers, credentials:'same-origin' });
  if (response.status === 401) { if (!path.includes('/auth/')) location.assign('/login'); throw new Error('Sessão expirada.'); }
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.mensagem || body.title || 'Não foi possível concluir a operação.');
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
export const money = (n:number = 0) => new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(n);
export const date = (v:string) => v ? new Intl.DateTimeFormat('pt-BR',{timeZone:'UTC'}).format(new Date(v + (v.length === 10 ? 'T00:00:00Z':''))) : '—';
export const nested = (obj:any, path:string) => path.split('.').reduce((x,k) => x?.[k],obj);
