# Progress
## 2026-09-25
- Rename via sed/git mv. zsh doesn't word-split `$files`, so the first sed failed harmlessly; reran with `${=files}`.
- A fifo-based server stdin deadlocked because opening for write blocks with no reader. Switched to `tail -f cmds.txt | server`.
- Run 1 (master build) created the world. Run 2 (branch) applied "1 remapping sets with a total of 11 remappings". CANNABIS 163/163, OVERDOSE 124/124.
