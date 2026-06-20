# glTFast Tweaks

<p><img width="747" height="310" alt="Screenshot 2026-06-20 at 10 53 20 PM" src="https://github.com/user-attachments/assets/af5625b5-bda1-4320-8a5a-e7fd1bb74ae4" /></p>

**glTFast Tweaks** is a Unity Editor extension that provides add-ons for the
[glTFast] package.

[glTFast]:
  https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.19/manual/index.html

## Features

The settings live in **Project Settings &gt; glTFast Tweaks**.

### Texture Overrides

**Texture Overrides** lets you override texture import settings per glTF asset.
It's especially convenient for `.glb` assets with embedded textures, which can
inflate memory and disk usage when those textures are high-resolution and
uncompressed.

When a glTF asset is imported, its embedded textures are adjusted according to
a set of override options:

- **Size**: longest-edge clamp (downscale)
- **Compression**: texture compression level
- **Filter**: texture filtering mode

The list is built by scanning the project; press the **Scan for glTF Assets**
button to rebuild it on demand.

## How to Install

The glTFast Tweaks package (`jp.keijiro.gltfast-tweaks`) can be installed via
the "Keijiro" scoped registry using Package Manager. To add the registry to
your project, please follow [these instructions].

[these instructions]:
  https://gist.github.com/keijiro/f8c7e8ff29bfe63d86b888901b82644c
