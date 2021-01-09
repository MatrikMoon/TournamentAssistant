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
### Windows
- Download and extract the latest `TournamentAssistantCore-release-windows.zip`
- Run `TournamentAssistantCore.exe`
![Screenshot 2021-01-09 172000](https://user-images.githubusercontent.com/44728973/104104290-74f28900-52a7-11eb-87d7-dfb1d98a4fba.png)

- After a window similar to the screenshot above appears, you can close it and there should be a new file named `serverConfig.json` in the server directory.
![Screenshot 2021-01-09 173840](https://user-images.githubusercontent.com/44728973/104104686-ecc1b300-52a9-11eb-9d42-6a484d9a8495.png)

- In this file, add you server address. It will need to be a domain name, direct IP addresses are not supported. **Dont forget to remove the brackets**
![Screenshot 2021-01-09 173306](https://user-images.githubusercontent.com/44728973/104104615-863c9500-52a9-11eb-97ee-a38372c9b972.png)

- You can also add a server name and password.
![Screenshot 2021-01-09 174423](https://user-images.githubusercontent.com/44728973/104104831-c5b7b100-52aa-11eb-907d-b2c668463f56.png)
![Screenshot 2021-01-09 174953](https://user-images.githubusercontent.com/44728973/104104970-7d4cc300-52ab-11eb-96e6-eb4a06d95719.png)

- Once thats done, you can save the changes and run `TournamentAssistantCore.exe` again.
![Screenshot 2021-01-09 175806](https://user-images.githubusercontent.com/44728973/104105167-aa4da580-52ac-11eb-8336-2f5a7921eca2.png)

- If the address got verified and port got opened, congratulations, you have just created your own server.
- If the address got verified but port could not be opened, you will need to open the selected port in your router settings manually. 
- If the address failed to verify and port got opened successfully and you made sure that there aren't any spelling mistakes or that the domain is pointed at the correct IP (keep in mind that DNS entries take up to an hour to be created/changed). 

### Linux
todo (the process is similar to windows so use that instead for now)


### TODO
Please help me improve this readme!

## Contributing
Pull requests are welcome! Feel free to dm me on discord if you have any questions or concerns!

## License
[MIT](https://choosealicense.com/licenses/mit/)
