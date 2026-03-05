import type { UiHtml } from '../core/FrameDecoder';

export class UiOverlay {
  private readonly _container: HTMLDivElement;
  private readonly _style: HTMLStyleElement;
  private _lastHtmlHash = '';
  private _lastCssHash = '';

  constructor(containerId = 'ui-overlay') {
    let el = document.getElementById(containerId) as HTMLDivElement | null;
    if (!el) {
      el = document.createElement('div');
      el.id = containerId;
      document.body.appendChild(el);
    }
    this._container = el;

    this._style = document.createElement('style');
    this._style.id = 'ui-overlay-style';
    document.head.appendChild(this._style);
  }

  update(uiHtml: UiHtml | undefined): void {
    if (!uiHtml) return;
    const { html, css } = uiHtml;

    if (html !== this._lastHtmlHash) {
      this._container.innerHTML = html;
      this._lastHtmlHash = html;
    }

    if (css !== this._lastCssHash) {
      this._style.textContent = css;
      this._lastCssHash = css;
    }
  }
}
