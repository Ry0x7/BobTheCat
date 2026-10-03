# Artwork

Bob retains the original ginger cat identity. Added art uses the create-pet reference-based image generation workflow, adapted to this standalone Windows application. The original photograph is private and excluded from the repository.

Each action was generated as a separate six-pose strip with the original canonical cat and photograph as references. Bundled component extraction separated the connected poses; all frames in a row received one shared nearest-neighbor scale and registration. The bundled compositor assembled the desktop atlas, followed by exactly one edge despill pass. No cat limbs or body pixels are drawn at runtime.

`source/action-sprites.png` has ten rows, six columns and 192x208 cells: loaf, stretch, scratch, groom, roll, window grip, fall, pounce, belly rub, head scratch. `source/yarn-sprites.png` has six 48x48 cells. `ExtraArt.cs` extracts those exact frames; the renderer scales them with nearest-neighbor sampling. The laser is a precise geometric target.

These are custom desktop atlases, not the ChatGPT Pets v2 upload layout. The bundled frame inspector passed all eleven final strips with no errors or warnings. Poses were visually checked for connected anatomy, consistency, clipping and action meaning; motion and idle transitions were rendered from final PNG pixels. The fall family contains midair and landing poses; actual vertical travel comes from the behavior engine.

The yarn requires a narrow chroma-distance threshold of 40 because the intentional purple color is close to magenta. Its final cleanup used a one-pixel edge band and strict spill similarity to preserve the pigment.

The build needs no image-generation service. These ready-to-use assets are embedded and work offline.

## Final asset hashes

- actions: `5bfff22a1ee477a85c69f7680193c8a2b489004ca767a07178d9a2dc519e67ba`
- yarn: `c31fdf7d147838e97244ae3b76781757aa6cbbfe215945e05b1f0a0e74cce124`
