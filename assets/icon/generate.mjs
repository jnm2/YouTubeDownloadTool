// Generates the app icon: one hand-tuned SVG per size, rendered to PNG, packed into AppIcon.ico.
// See README.md in this folder for how to adjust and regenerate.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(HERE, 'out');
const ICO = path.join(HERE, '..', '..', 'src', 'YouTubeDownloadTool', 'AppIcon.ico');
const PNG = path.join(HERE, 'sizes.png'); // committed, shown in README.md
// Committed, shown inline in the repository README.md's h1: the 32 px form, sized so that the tray bottom sits on the
// text baseline and the top of the arrowhead lines up with the cap height (GitHub h1 is 32px Segoe UI Semibold on
// Windows, cap height 22.5px; macOS and Linux fonts differ by about 1px).
const README_SVG = path.join(HERE, '..', 'readme', 'icon.svg');
const README_FORM = 32, README_CAP_HEIGHT = 22.5;

const programFiles = process.env.ProgramFiles ?? 'C:\\Program Files';
const findTool = (envVar, candidates, fallback) =>
  process.env[envVar] ?? candidates.find((c) => fs.existsSync(c)) ?? fallback;
const INKSCAPE = findTool('INKSCAPE', [path.join(programFiles, 'Inkscape', 'bin', 'inkscape.com')], 'inkscape');
const MAGICK = findTool('MAGICK', (fs.existsSync(programFiles) ? fs.readdirSync(programFiles) : [])
  .filter((d) => d.startsWith('ImageMagick')).map((d) => path.join(programFiles, d, 'magick.exe')), 'magick');
const run = (exe, args) => execFileSync(exe, args, { cwd: OUT, stdio: ['ignore', 'pipe', 'pipe'] });

const SIZES = [16, 20, 24, 32, 48, 64, 256];

// Colors
const BODY = ['#F472B6', '#C026D3', '#7C3AED'];  // arrow + tray ("berry")
const STRIP = ['#A21CAF', '#7E22CE', '#6D28D9']; // film strip ("grape")
const FRAME_FILL = '#F9A8D4', FRAME_OPACITY = 0.5; // film frames: translucent pink over the strip; perforations are cut out

// 16, 20 and 24 are placed pixel by pixel (16 = 100% title bar, 20 = 125%, 24 = 150%).
// s = strip [x0, y0, x1, y1]; holeX/holeY = perforation positions; frames = [y0, y1] spans within x range fx;
// head = arrowhead triangle; tray = [x0, y0, x1, y1, wall, bottom, cornerRadius].
const PIXEL_TIERS = {
  16: { s: [4, 0, 12, 8], holeX: [5, 10], holeW: 1, holeY: [1, 3, 5], holeH: 1, frames: [[1, 3], [4, 6]], fx: [7, 9],
    head: [[0.5, 6], [15.5, 6], [8, 13]], hr: 0, tray: [0, 10, 16, 16, 2, 2, 1] },
  20: { s: [5, 0, 15, 9], holeX: [6, 13], holeW: 1, holeY: [1, 3, 5, 7], holeH: 1, frames: [[1, 4], [5, 8]], fx: [8, 12],
    head: [[0.5, 8], [19.5, 8], [10, 16.5]], hr: 0, tray: [0, 13, 20, 20, 2, 2, 1] },
  24: { s: [7, 0, 17, 11], holeX: [8, 15], holeW: 1, holeY: [1, 3, 5, 7, 9], holeH: 1, frames: [[1, 4], [5, 8], [9, 11]], fx: [10, 14],
    head: [[0.5, 10], [23.5, 10], [12, 20]], hr: 0, tray: [0, 15, 24, 24, 3, 3, 1] },
};

// 32 and up: proportions (fractions of the canvas) blend from AT16 toward AT256 on a log2 scale,
// then snap to whole pixels. This lets the film grow and the arrow ease as the icon gets bigger.
// sw strip width · hw/hh perforation size · margin strip edge→perforation · inner perforation→frame · pitch perforation spacing
// ppf perforations per frame · fgap gap between frames · phase vertical offset of perforations · headTop/headHW/headH arrowhead
// trayX/trayTop/trayBot tray box · wall/bt tray wall/bottom thickness · rr tray corner radius
const AT16 = { sw: 0.5, hw: 0.0625, hh: 0.0625, margin: 0.0625, pitch: 0.125, inner: 0.0625, ppf: 1.5, fgap: 0.0625, phase: 0.0625,
  headTop: 0.375, headHW: 0.47, headH: 0.44, trayX: 0, trayTop: 0.625, trayBot: 1, wall: 0.125, bt: 0.125, rr: 0.0625 };
const AT256 = { sw: 0.46, hw: 0.05, hh: 0.038, margin: 0.03, pitch: 0.075, inner: 0.03, ppf: 3, fgap: 0.03, phase: -0.02,
  headTop: 0.47, headHW: 0.40, headH: 0.36, trayX: 0.04, trayTop: 0.64, trayBot: 0.95, wall: 0.075, bt: 0.075, rr: 0.035 };

// Arrowhead corner radius in px.
const HEAD_R = { 32: 1, 48: 1.5, 64: 2.5, 256: 7 };
// Per-size pixel overrides where the blended proportions round awkwardly (keeps film frames ~1.2–1.33 wide:tall).
const PX_OVERRIDE = {
  32: { margin: 1, inner: 2, fgap: 3 },
  48: { holeH: 2, inner: 3, fgap: 3 },
  64: { fgap: 4 },
};

const lerp = (a, b, t) => a + (b - a) * t;
function geom(G) {
  const t = (Math.log2(G) - 4) / 4;
  const f = Object.fromEntries(Object.keys(AT16).map((k) => [k, lerp(AT16[k], AT256[k], t)]));
  const o = PX_OVERRIDE[G] ?? {};
  const R = Math.round, px = (v, min = 1) => Math.max(min, R(v * G));
  const sw = 2 * R(f.sw * G / 2), x0 = (G - sw) / 2, x1 = x0 + sw;
  const holeW = o.holeW ?? px(f.hw), holeH = o.holeH ?? px(f.hh), margin = o.margin ?? px(f.margin), inner = o.inner ?? px(f.inner);
  const pitch = Math.max(holeH + 1, px(f.pitch, 2));
  const headTop = R(f.headTop * G), headHW = R(f.headHW * G * 2) / 2, headH = R(f.headH * G);
  // The strip ends where the arrowhead is still wider than it, so its bottom stays hidden.
  const stripBot = headTop + Math.max(1, Math.floor((headHW - sw / 2) * headH / headHW) - 1);
  const phase = R(f.phase * G);
  const holeY = [];
  for (let y = phase; y < headTop; y += pitch) if (y + holeH > 0) holeY.push(y);
  const fpitch = Math.max(1, R(f.ppf)) * pitch, fgap = o.fgap ?? px(f.fgap);
  const frames = [];
  for (let y = phase; y < headTop; y += fpitch) if (y + fpitch - fgap > 0) frames.push([y, Math.min(stripBot, y + fpitch - fgap)]);
  const tx = R(f.trayX * G);
  return {
    s: [x0, -R(0.06 * G) - 1, x1, stripBot], holeX: [x0 + margin, x1 - margin - holeW], holeW, holeY, holeH,
    frames, fx: [x0 + margin + holeW + inner, x1 - margin - holeW - inner],
    head: [[G / 2 - headHW, headTop], [G / 2 + headHW, headTop], [G / 2, headTop + headH]], hr: HEAD_R[G] ?? 0,
    tray: [tx, R(f.trayTop * G), G - tx, R(f.trayBot * G), o.wall ?? px(f.wall, 2), o.bt ?? px(f.bt, 2), Math.max(1, f.rr * G)],
    holeR: G >= 64 ? holeH * 0.35 : 0, frameR: G >= 48 ? G * 0.012 : 0, stripR: G * 0.03,
  };
}

// Insets a convex polygon by d, so that a round stroke of width 2d restores the outline with rounded corners.
function inset(pts, d) {
  if (!d) return pts;
  const n = pts.length, lines = [];
  const cw = pts.reduce((a, p, i) => a + (pts[(i + 1) % n][0] - p[0]) * (pts[(i + 1) % n][1] + p[1]), 0) > 0 ? -1 : 1;
  for (let i = 0; i < n; i++) {
    const [ax, ay] = pts[i], [bx, by] = pts[(i + 1) % n];
    const len = Math.hypot(bx - ax, by - ay);
    lines.push([ax + (-(by - ay) / len) * cw * d, ay + ((bx - ax) / len) * cw * d, bx - ax, by - ay]);
  }
  return lines.map((l, i) => {
    const [px1, py1, dx1, dy1] = lines[(i + n - 1) % n], [px2, py2, dx2, dy2] = l;
    const s = ((px2 - px1) * dy2 - (py2 - py1) * dx2) / (dx1 * dy2 - dy1 * dx2);
    return [+(px1 + dx1 * s).toFixed(3), +(py1 + dy1 * s).toFixed(3)];
  });
}

const hex2rgb = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const rgb2hex = (c) => '#' + c.map((v) => Math.round(v).toString(16).padStart(2, '0')).join('').toUpperCase();
const mix = (a, b, t) => rgb2hex(hex2rgb(a).map((v, i) => v + (hex2rgb(b)[i] - v) * t));
const grad = (id, stops, G) => `<linearGradient id="${id}" gradientUnits="userSpaceOnUse" x1="${0.3 * G}" y1="0" x2="${0.7 * G}" y2="${G}">`
  + stops.map((c, i) => `<stop offset="${i / (stops.length - 1)}" stop-color="${c}"/>`).join('') + '</linearGradient>';
const rrect = (x0, y0, x1, y1, r, attrs = '') => `<rect x="${x0}" y="${y0}" width="${x1 - x0}" height="${y1 - y0}" rx="${r}" ${attrs}/>`;
const tri = (pts, r, fill) => {
  const points = inset(pts, r).map((p) => p.join(',')).join(' ');
  return r > 0
    ? `<polygon points="${points}" fill="${fill}" stroke="${fill}" stroke-width="${2 * r}" stroke-linejoin="round"/>`
    : `<polygon points="${points}" fill="${fill}"/>`;
};
const trayPath = ([x0, y0, x1, y1, w, bt, rr]) =>
  `M${x0},${y0} V${y1 - rr} Q${x0},${y1} ${x0 + rr},${y1} H${x1 - rr} Q${x1},${y1} ${x1},${y1 - rr} V${y0} H${x1 - w} V${y1 - bt} H${x0 + w} V${y0} Z`;

function draw(G, p) {
  const [x0, y0, x1, y1] = p.s;
  const stripR = p.stripR ?? 0;
  let holes = '', frames = '';
  for (const y of p.holeY) for (const x of p.holeX) holes += rrect(x, y, x + p.holeW, y + p.holeH, p.holeR ?? 0);
  for (const [a, c] of p.frames) frames += rrect(p.fx[0], a, p.fx[1], c, p.frameR ?? 0);
  const hasShadow = G >= 32;
  let defs = grad('body', BODY, G) + grad('strip', STRIP, G)
    + `<mask id="cut" maskUnits="userSpaceOnUse" x="0" y="0" width="${G}" height="${G}"><rect width="${G}" height="${G}" fill="#fff"/><g fill="#000">${holes}</g></mask>`;
  if (hasShadow) defs += `<filter id="sh" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="${(G / 48).toFixed(2)}"/></filter>`
    + `<clipPath id="cStrip">${rrect(x0, y0, x1, y1, stripR)}</clipPath><clipPath id="cTray"><path d="${trayPath(p.tray)}"/></clipPath>`;
  // Soft shadow of the arrowhead, falling on the strip and tray only.
  const shadow = (clip) => hasShadow
    ? `<g clip-path="url(#${clip})" opacity="0.35"><g transform="translate(0 ${+(G / 48).toFixed(2)})" filter="url(#sh)">${tri(p.head, p.hr, mix(BODY.at(-1), '#000000', 0.7))}</g></g>`
    : '';
  const body = `<path d="${trayPath(p.tray)}" fill="url(#body)"/>` + shadow('cTray')
    + `<g mask="url(#cut)">${rrect(x0, y0, x1, y1, stripR, 'fill="url(#strip)"')}${shadow('cStrip')}</g>`
    + `<g fill="${FRAME_FILL}" fill-opacity="${FRAME_OPACITY}">${frames}</g>`
    + tri(p.head, p.hr, 'url(#body)');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${G}" height="${G}" viewBox="0 0 ${G} ${G}"><defs>${defs}</defs>${body}</svg>\n`;
}

fs.mkdirSync(OUT, { recursive: true });
for (const G of SIZES) fs.writeFileSync(path.join(OUT, `icon-${G}.svg`), draw(G, PIXEL_TIERS[G] ?? geom(G)));
run(INKSCAPE, [`--actions=${SIZES.map((s) => `file-open:icon-${s}.svg;export-width:${s};export-height:${s};export-filename:icon-${s}.png;export-do;file-close`).join(';')}`]);
run(MAGICK, [...SIZES.map((s) => `icon-${s}.png`), ICO]);

// Crop the viewBox at the tray bottom (so it lands on the baseline, since images are baseline-aligned) and scale the
// span from arrowhead top to tray bottom to the cap height.
{
  const G = README_FORM, p = PIXEL_TIERS[G] ?? geom(G);
  const top = Math.min(...p.head.map(([, y]) => y)), bottom = p.tray[3];
  const scale = README_CAP_HEIGHT / (bottom - top), size = (v) => +(v * scale).toFixed(3);
  const from = `width="${G}" height="${G}" viewBox="0 0 ${G} ${G}"`;
  const svg = draw(G, p);
  if (!svg.includes(from)) throw new Error('Unexpected SVG header');
  fs.writeFileSync(README_SVG, svg.replace(from, `width="${size(G)}" height="${size(bottom)}" viewBox="0 0 ${G} ${bottom}"`));
}

// Every size at actual pixel size, side by side on a transparent background, bottom-aligned.
run(MAGICK, ['-background', 'none', ...SIZES.map((s) => `icon-${s}.png`), '-bordercolor', 'none', '-border', '8', '-gravity', 'south', '+append', '+repage', PNG]);

// Preview: every size enlarged to 160px with nearest-neighbor scaling, so each pixel is visible.
run(MAGICK, ['-background', '#F3F3F3', ...SIZES.flatMap((s) => ['(', `icon-${s}.png`, '-filter', s === 256 ? 'Lanczos' : 'point', '-resize', '160x160', ')']),
  '-bordercolor', '#F3F3F3', '-border', '8', '+append', '-alpha', 'remove', 'preview.png']);

console.log(`Wrote ${path.relative(process.cwd(), ICO)} (${SIZES.join(', ')}), ${path.relative(process.cwd(), PNG)}, ${path.relative(process.cwd(), README_SVG)} and ${path.relative(process.cwd(), path.join(OUT, 'preview.png'))}`);
