// jsdom không có ResizeObserver; popup của Headless UI (SelectField, SearchSelect) cần nó để định vị.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver = globalThis.ResizeObserver ?? ResizeObserverStub

export {}
