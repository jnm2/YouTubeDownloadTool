# Social preview

`generate.mjs` is the source of the repository's social preview image, the card shown when a link to the repository is shared on Slack, Discord, social media and so on. It builds the card as an HTML page from the app icon and [`assets/readme/downloading.png`](../readme/downloading.png), and renders it to a 1280×640 PNG with headless Edge.

The PNG isn't committed. It's uploaded to GitHub instead.

## Regenerating

Requires [Node.js](https://nodejs.org) and Microsoft Edge or Google Chrome. The browser is found in its default install location, on `PATH` as `msedge`, or via the `EDGE` environment variable (any Chromium-based browser works).

The card uses the 256 px icon from the icon generator's intermediate output, so run that first (see [`assets/icon/README.md`](../icon/README.md) for its requirements):

```
node assets/icon/generate.mjs
node assets/social/generate.mjs
```

This writes to `assets/social/out/`:

- `social-preview.png`: the image to upload.
- `social-preview-guides.png`: the same card with a dashed line marking the 80 px border that GitHub recommends keeping important content inside, since some sites crop the edges.
- `social-preview.html` and `social-preview-guides.html`: the pages that were rendered. Open them in a browser to try changes live with the developer tools.

The fonts are Segoe UI Variable, so render on Windows to get the intended look.

## Uploading

In the repository on GitHub, go to **Settings → General → Social preview → Edit → Upload an image** and choose `assets/social/out/social-preview.png`. The image must be under 1 MB.

## Adjusting

- **Text, layout and sizes**: the `card` template and its CSS.
- **Colors**: `BG`, `TITLE`, `TAGLINE`, `NOTE` and `GLOW`, picked to match the icon's colors.
- **Screenshot**: `SCREENSHOT`, shared with the [repository README](../../README.md). If it's retaken, regenerate the card and upload it again.

Check `social-preview-guides.png` after any change to make sure the text and screenshot stay inside the dashed line.
