# StringTheory

<img width="1730" height="959" alt="image" src="https://github.com/user-attachments/assets/b35a2f5e-64b5-48b0-81a7-d8d23a79169a" />

StringTheory is a guitar game built in Unity that turns practice into something closer to a rhythm game.

You can load basically any song, pick the track you want, and play along while the game listens in real time.

Live note and chord detection powers the scoring system, so your performance is tracked while you play and practice feels competitive and fun.

## What it does today

- Live note detection and chord detection while you play
 
- A scoring system so you can track how well you are doing
  
- Looping for any section you select
  <img width="909" height="297" alt="image" src="https://github.com/user-attachments/assets/beb40feb-fdd1-4906-b701-f17a2d88745e" />

- Slow down playback so hard parts are easier to learn
  
- Timing offset controls by track and by full song
  
- Instant track switching inside the same song
  <img width="565" height="539" alt="image" src="https://github.com/user-attachments/assets/5c3100e5-9cd2-4a56-a0ea-2a1fc824ddd6" />
  
- Lots of settings for gameplay and practice behavior
  
- Early 3D view work has started, but it is still incomplete

There is also a simple amp simulator app included in the project.

## Adding songs

Adding songs is intentionally very easy.

1. Create a folder inside the `songs` folder.
2. Put your `MusicXML` file in that folder.
3. Optionally add an `mp3` file in the same folder.

That is it. The song will show up directly in the game library.

If you prefer, you can open the songs folder from inside the game using the folder button in the library.

## Guitar Pro files (.gp)

If you have a `.gp` file, you can convert it to MusicXML in a minute.

Use a free tool such as TuxGuitar, open the `.gp` file, then export it as MusicXML.

The exported file works directly in StringTheory and loads tracks and guitar techniques automatically.
