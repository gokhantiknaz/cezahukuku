import { test } from "node:test";
import assert from "node:assert/strict";
import { createElement as h } from "react";
import { renderToString } from "react-dom/server";
import { Group, Panel } from "react-resizable-panels";
import { parseSavedLayout, useWorkspaceLayout, WORKSPACE_LAYOUT_KEY } from "./workspace-layout.ts";

// Bug 1: masaüstü çalışma alanında sunucu panelleri varsayılan genişlikle çiziyor, tarayıcı hidrasyon sırasında
// localStorage'daki kayıtlı genişlikle; React "hydration mismatch" (ResizablePanel style) uyarısı veriyor.
// renderToString, useSyncExternalStore'un sunucu anlık görüntüsünü kullanır: hidrasyon çizimi de onu kullanır.
// Böylece "window varken" yapılan çizim tarayıcının hidrasyon çizimiyle aynıdır.

type Storage = { getItem(key: string): string | null; setItem(key: string, value: string): void };

function WorkspaceGroup() {
  const layout = useWorkspaceLayout();
  return h(
    Group,
    { orientation: "horizontal", ...layout },
    h(Panel, { id: "navigation", defaultSize: "24", minSize: 260, maxSize: "40" }),
    h(Panel, { id: "content", minSize: "35" }),
    h(Panel, { id: "notes", defaultSize: "22", minSize: 260, maxSize: "35", collapsible: true, collapsedSize: 0 }),
  );
}

function renderInBrowser(localStorage: Storage) {
  const global = globalThis as { window?: unknown };
  global.window = { localStorage };
  try {
    return renderToString(h(WorkspaceGroup));
  } finally {
    delete global.window;
  }
}

function storageWith(entries: Record<string, string>): Storage {
  const map = new Map(Object.entries(entries));
  return { getItem: (key) => map.get(key) ?? null, setItem: (key, value) => void map.set(key, value) };
}

test("kayıtlı panel genişliği hidrasyon çizimini sunucu çiziminden ayırmaz", () => {
  const server = renderToString(h(WorkspaceGroup));
  const saved = storageWith({
    [WORKSPACE_LAYOUT_KEY]: JSON.stringify({ navigation: 30, content: 50, notes: 20 }),
  });

  const hydration = renderInBrowser(saved);

  assert.equal(hydration, server);
});

test("kayıt anahtarı kütüphanenin önceki kayıtlarıyla aynı: kullanıcının ayarladığı genişlik kaybolmaz", () => {
  assert.equal(WORKSPACE_LAYOUT_KEY, "react-resizable-panels:workspace");
  assert.deepEqual(parseSavedLayout(JSON.stringify({ navigation: 30, content: 50, notes: 20 })), {
    navigation: 30,
    content: 50,
    notes: 20,
  });
  assert.deepEqual(parseSavedLayout(JSON.stringify({ navigation: 24, content: 76, notes: 0 })), {
    navigation: 24,
    content: 76,
    notes: 0,
  });
});

test("bozuk ya da beklenmeyen kayıt yok sayılır", () => {
  for (const raw of [null, "", "bozuk", "null", "[]", "42", "{}", '{"navigation":"30"}', '{"navigation":null}'])
    assert.equal(parseSavedLayout(raw), undefined, String(raw));
});
