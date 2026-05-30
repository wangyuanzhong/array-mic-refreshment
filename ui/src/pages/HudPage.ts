/** Minimal overlay for the native WebView2 voice status HUD (#/hud). */
export function mountHudPage(root: HTMLElement): void {
  document.body.classList.add('hud-mode');
  root.innerHTML = `
    <div class="voice-hud" id="voiceHud" hidden>
      <span class="voice-hud__dot" id="voiceHudDot" aria-hidden="true"></span>
      <span class="voice-hud__text" id="voiceHudText"></span>
    </div>
  `;

  const hud = root.querySelector<HTMLElement>('#voiceHud');
  const textEl = root.querySelector<HTMLElement>('#voiceHudText');
  if (!hud || !textEl) {
    return;
  }

  const webview = (window as Window & { chrome?: { webview?: { addEventListener: typeof window.addEventListener } } })
    .chrome?.webview;

  const apply = (phase: string, message: string) => {
    if (!phase || phase === 'Idle' || !message.trim()) {
      hud.hidden = true;
      return;
    }

    hud.hidden = false;
    hud.dataset.phase = phase;
    textEl.textContent = message;
  };

  if (webview) {
    webview.addEventListener('message', (event: Event) => {
      try {
        const detail = (event as MessageEvent<string>).data;
        const payload = typeof detail === 'string' ? JSON.parse(detail) : detail;
        apply(String(payload.phase ?? ''), String(payload.message ?? ''));
      } catch {
        /* ignore malformed host messages */
      }
    });
  }
}
