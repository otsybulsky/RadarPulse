export function readCurrentUrl(): URL | null {
  try {
    return new URL(globalThis.location.href);
  } catch {
    return null;
  }
}
