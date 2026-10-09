// Generates the repository's social preview image (Settings → General → Social preview): an HTML card rendered to a
// 1280×640 PNG by headless Edge. See README.md in this folder for how to adjust and regenerate.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(HERE, 'out');
const ICON = path.join(HERE, '..', 'icon', 'out', 'icon-256.svg'); // written by assets/icon/generate.mjs
const SCREENSHOT = path.join(HERE, '..', 'readme', 'downloading.png');

const W = 1280, H = 640;
// GitHub's template asks for a 40pt (80px) border around anything important, since some sites crop the edges.
const SAFE = 80;

const findTool = (envVar, candidates, fallback) =>
  process.env[envVar] ?? candidates.find((c) => fs.existsSync(c)) ?? fallback;
const EDGE = findTool('EDGE', [
  ...[process.env['ProgramFiles(x86)'], process.env.ProgramFiles].filter(Boolean).flatMap((pf) => [
    path.join(pf, 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
    path.join(pf, 'Google', 'Chrome', 'Application', 'chrome.exe'),
  ]),
  '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
], 'msedge');

// Colors (from the icon's BODY gradient in assets/icon/generate.mjs)
const BG = 'radial-gradient(900px 600px at 50% 0%, #3b1260 0%, #1a0b2e 55%, #0e0718 100%)';
const TITLE = '#FFFFFF', TAGLINE = '#F9A8D4', NOTE = '#A78BBA', GLOW = 'rgba(192, 38, 211, .45)';

const card = (guides) => `<!doctype html>
<meta charset="utf-8">
<style>
  * { box-sizing: border-box; margin: 0; }
  html, body { width: ${W}px; height: ${H}px; overflow: hidden; }
  body {
    font-family: 'Segoe UI Variable Display', 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
    background: ${BG};
    display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center;
  }
  img { display: block; }
  .heading { display: flex; align-items: flex-end; gap: 18px; }
  .heading img { margin-bottom: 6px; filter: drop-shadow(0 8px 24px ${GLOW}); }
  h1 { font-size: 64px; font-weight: 650; letter-spacing: -0.02em; line-height: 1.05; color: ${TITLE}; }
  .tagline { font-size: 28px; color: ${TAGLINE}; margin-top: 12px; }
  .note { font-size: 20px; color: ${NOTE}; margin-left: 0.5em; }
  .window {
    margin-top: 40px; border-radius: 8px;
    box-shadow: 0 0 0 1px rgba(255, 255, 255, .12), 0 30px 80px rgba(0, 0, 0, .6), 0 0 120px rgba(192, 38, 211, .25);
  }
  .guides { position: absolute; inset: ${SAFE}px; outline: 2px dashed rgba(255, 0, 0, .8); }
</style>
<div class="heading">
  <img src="${pathToFileURL(ICON)}" width="76" height="76" alt="">
  <h1>YouTube download tool</h1>
</div>
<p class="tagline">Paste a link. Get the video.<span class="note">1,000+ sites, powered by yt-dlp + FFmpeg</span></p>
<img class="window" src="${pathToFileURL(SCREENSHOT)}" width="530" height="250" alt="">
${guides ? '<div class="guides"></div>' : ''}
`;

if (!fs.existsSync(ICON)) throw new Error(`${path.relative(process.cwd(), ICON)} not found; run assets/icon/generate.mjs first.`);
fs.mkdirSync(OUT, { recursive: true });

// A throwaway profile keeps this from touching (or waiting on) an already-running browser.
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'social-preview-'));
try {
  for (const [name, guides] of [['social-preview', false], ['social-preview-guides', true]]) {
    const html = path.join(OUT, `${name}.html`), png = path.join(OUT, `${name}.png`);
    fs.writeFileSync(html, card(guides));
    fs.rmSync(png, { force: true });
    execFileSync(EDGE, ['--headless', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1',
      `--user-data-dir=${profile}`, `--window-size=${W},${H}`, `--screenshot=${png}`, pathToFileURL(html).href],
      { stdio: ['ignore', 'pipe', 'pipe'] });
    if (!fs.existsSync(png)) throw new Error(`${EDGE} did not write ${png}`);
  }
} finally {
  fs.rmSync(profile, { recursive: true, force: true });
}

console.log(`Wrote ${path.relative(process.cwd(), path.join(OUT, 'social-preview.png'))} and social-preview-guides.png`);
