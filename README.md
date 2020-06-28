THIS README IS INCOMPLETE! FEEL FREE TO HELP ME ADD TO IT!
# TournamentAssistant
A program designed to make it easier to coordinate tournaments for the VR rhythm game Beat Saber

## How To Coordinate a Match
- Download the latest release build (`TournamentAssistantUI.exe`) from Releases

### Usage
- Run `TournamentAssistantUI.exe`
- Enter the server address/port in the “Host IP” box
- Enter your username in the “Username” box and click Connect
- Click on the desired players’ names so that they become highlighted and then press the blue “Create Match” button
- When in the Match Room, paste the desired song link or key into the “Song URL” box
- Click “Load Song” and wait for a the song to load (If you look closely, you’ll notice the button itself is the progress bar)
- Select your desired characteristic (standard, one saber, etc.), difficulty (easy-expert+), and modifiers (no fail, faster song, etc.)
- Click the “Play Song” button

 **With Stream Sync (Optional)**
- Instead of clicking the “Play Song” button, click “Play Song (With Dual Sync)”
- You will see a green box highlighting one of your monitors. Be sure the streams you want to sync are visible insdie that monitor
- Note: If the matches are streamed to a central channel (as for most tournaments) you will have to use that channel for Sync. Do not sync by pulling up the players' individual streams

## How to Host a Server
- Download the latest release build (`TournamentAssistantCore-release-[platform].zip`) from Releases and extract it to any location
- Run `TournamentAssistantCore.exe` (or the linux equivalent) once to generate `serverConfig.json`
- Modify `serverConfig.json` to set your desired `port` and `ServerName`
- Run `TournamentAssistantCore.exe` again!
- (You may also change other settings and add teams, an overlay, or a list of banned mods!)

### TODO
Please help me improve this readme!

## Contributing
Pull requests are welcome! Feel free to dm me on discord if you have any questions or concerns!

## License
[MIT](https://choosealicense.com/licenses/mit/)
