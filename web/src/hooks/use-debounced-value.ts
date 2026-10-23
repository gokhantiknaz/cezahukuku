import { useEffect, useState } from "react";

/** Değer belirtilen süre boyunca değişmeyince güncellenir (yazarken her tuşta istek atılmasın). */
export function useDebouncedValue<T>(value: T, delayMs: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}
