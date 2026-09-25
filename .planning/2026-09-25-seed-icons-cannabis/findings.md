# Findings
- Missing sprite = `textures/item/poppy-seeds.png` was one flat color (60,50,40). All three seed items shared it.
- Vanilla seeds: shape `item/resource/seeds/seedbag`, texture keys normal/reedrope/seed. The seed label is a 16x16 plant picture with a 1px black outline. Decision: reuse the vanilla shape so our seeds match vanilla and read as seeds in the handbook.
- Cannabis leaf drawn as ellipses merges into a blob at 16px, so the pixels are hand-placed.
- Remaps: `ServerSystemRemapperAssistant` merges every domain's `config/remaps.json` and auto-applies unseen sets (unless DisableAutoRemap). `/iir remapq <new> <old>`: AutoRemap(array[3]=old, array[2]=new).
- Tolerance attribute key changes from marijuana to cannabis. Old counters are dropped; accepted as minor.
- tools/build_cannabis_models.py used to overwrite cannabis-seeds.json with the placeholder. That write was removed.
