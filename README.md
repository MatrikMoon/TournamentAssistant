THIS README IS INCOMPLETE! FEEL FREE TO HELP ME ADD TO IT!
# TournamentAssistant
A program designed to make it easier to coordinate tournaments for the VR rhythm game Beat Saber

### (You do not need to host your own server if you don't want to. Feel free to use the Default Server!)

### Contents:
  - How to install the plugin
  - How to coordinate a match
      - Basic
      - With stream sync
  - How to host a server
      - Windows
      - Linux
          - Bash script (TBA)
          - Terminal only
          - GUI

<details>
<summary> Expand to show installation instructions </summary>

## How to install the plugin
- Download the [latest version of the .dll from the repository](https://github.com/MatrikMoon/TournamentAssistant/releases/latest) or from your tournament coordinators (e.g. Beat The Hub discord).

- After downloading the .dll open your steam client, right-click BeatSaber, goto manage, and click Browse local files.
![Screenshot 2021-02-03 141715](https://user-images.githubusercontent.com/44728973/106752470-c7b32c80-662a-11eb-8cd1-fbe0a947f12d.png)
![Screenshot 2021-02-03 141735](https://user-images.githubusercontent.com/44728973/106752474-c97cf000-662a-11eb-8ae3-a9f00bd64cb4.png)
![Screenshot (12)](https://user-images.githubusercontent.com/44728973/106752477-caae1d00-662a-11eb-9882-a368d2a5f1e6.png)

- After that, a window with the game files should open, when it does, open the location where you downloaded the .dll (Downloads folder by default) and drag n' drop it into the plugins folder.
![Screenshot 2021-02-03 142206](https://user-images.githubusercontent.com/44728973/106752977-57f17180-662b-11eb-8ac9-95650ad65125.png)
![Screenshot 2021-02-03 142227](https://user-images.githubusercontent.com/44728973/106752981-588a0800-662b-11eb-9a6f-059499ae05a8.png)
![Screenshot (13)](https://user-images.githubusercontent.com/44728973/106752984-5a53cb80-662b-11eb-8a20-aa52bf8eb5d5.png)

- After doing that, you should be good to go. If you encounter any issues, feel free to ask for help on [BSMG](https://discord.gg/beatsabermods) or ask your tournament coordinators.


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
- Download and extract the latest `TournamentAssistantCore-windows.zip`
- Run `TournamentAssistantCore.exe`
![Screenshot 2021-01-09 172000](https://user-images.githubusercontent.com/44728973/104104290-74f28900-52a7-11eb-87d7-dfb1d98a4fba.png)

- After a window similar to the screenshot above appears, you can close it and there should be a new file named `serverConfig.json` in the server directory.
![Screenshot 2021-01-09 173840](https://user-images.githubusercontent.com/44728973/104104686-ecc1b300-52a9-11eb-9d42-6a484d9a8495.png)

- In this file, add your server address. It will need to be a domain name, direct IP addresses are not supported. **Don't forget to remove the brackets**
![Screenshot 2021-01-09 173306](https://user-images.githubusercontent.com/44728973/104104615-863c9500-52a9-11eb-97ee-a38372c9b972.png)

- You can also add a server name and password.
![Screenshot 2021-01-09 174423](https://user-images.githubusercontent.com/44728973/104104831-c5b7b100-52aa-11eb-907d-b2c668463f56.png)
![Screenshot 2021-01-09 174953](https://user-images.githubusercontent.com/44728973/104104970-7d4cc300-52ab-11eb-96e6-eb4a06d95719.png)

- Once thats done, you can save the changes and run `TournamentAssistantCore.exe` again.
![Screenshot 2021-01-09 175806](https://user-images.githubusercontent.com/44728973/104105167-aa4da580-52ac-11eb-8336-2f5a7921eca2.png)

- If the address got verified and port got opened, congratulations, you have just created your own server.
- If the address got verified but port could not be opened, you will need to open the selected port in your router settings manually. 
- If the address failed to verify and port got opened successfully and you made sure that there aren't any spelling mistakes or that the domain is pointed at the correct IP (keep in mind that DNS entries take up to an hour to be created/changed) seek help on discord.

### Linux
#### Bash script
TBA

#### Terminal only
I'll be using Arch Linux with neovim in this example. I won't provide screenshots here since I'll be assuming that you know what you're doing. If you don't, then I would recommend the GUI guide below.
- Make a new directory where you want to save the server files and cd to it:`mkdir TournamentAssistantCore && cd TournamentAssistantCore`

- Download the latest `TournamentAssistantCore-linux` using wget: `wget https://github.com/MatrikMoon/TournamentAssistant/releases/download/0.4.1/TournamentAssistantCore-linux`. *This example command is for the version 0.4.1, if a new version has been released please replace the url with the new one*

- Make `TournamentAssistantCore-linux` executable using `chmod +x /path/to/TournamentAssistantCore-linux`.

- Run `TournamentAssistantCore-linux` using `./TournamentAssistantCore-linux`. After ~5 seconds close it with `ctrl + c`.

- Open your terminal editor of choice and edit the configuration file: `nvim serverConfig.json`. You can use the screenshots from Linux GUI setup as a reference. **Don't forget to remove the brackets**.  
    - Add your server address. It will need to be a domain name, direct IP addresses are not supported.
    - You can also add a server name and password.
    
- Run `TournamentAssistantCore-linux` using `./TournamentAssistantCore-linux` again.

- If the address got verified and port got opened, congratulations, you have just created your own server.
- If the address got verified but port could not be opened, you will need to open the selected port in your router settings manually. 
- If the address failed to verify and port got opened successfully and you made sure that there aren't any spelling mistakes or that the domain is pointed at the correct IP (keep in mind that DNS entries take up to an hour to be created/changed) seek help on discord.

#### GUI
I'll be using Arch Linux with KDE DE in this example
- Download the latest `TournamentAssistantCore-linux`. I'd recommend using your home directory, just so you don't have to deal with sudo.
- Make `TournamentAssistantCore-linux` executable using your file manager of choice or using `chmod +x /path/to/TournamentAssistantCore-linux`. (In the screenshot I am using nautilus)
![Screenshot_20210203_092620](https://user-images.githubusercontent.com/44728973/106719154-3f209600-6602-11eb-8dff-9772e295de6a.png)
![Screenshot_20210203_092821](https://user-images.githubusercontent.com/44728973/106719162-40ea5980-6602-11eb-9164-7ebc62a68d1a.png)
- Open terminal and run `./TournamentAssistantCore-linux`. If you saved the executable somewhere else you will have to either specify the path to it `./path/to/TournamentAssistantCore-linux` or cd into the directory and then run `./TournamentAssistantCore-linux`. 

**Do not doubleclick the executable, it will run headless and you won't be able to turn it off without killing the process in the terminal or rebooting.**

*in this example I have TournamentAssistantCore-linux in a subfolder in my home directory*
![Screenshot_20210203_103524](https://user-images.githubusercontent.com/44728973/106727262-9414da00-660b-11eb-8bc5-879103bf80cf.png)
- Now press `ctrl + c` to close TournamentAssistantCore-linux and use your text/code editor of choice to edit the `serverConfig.json` configuration file. (In the screenshots I am using VS Code)
![Screenshot_20210203_105623](https://user-images.githubusercontent.com/44728973/106730110-83b22e80-660e-11eb-97e0-ee0438470075.png)

- In this file, add your server address. It will need to be a domain name, direct IP addresses are not supported. **Don't forget to remove the brackets**.               
![Screenshot_20210203_105340](https://user-images.githubusercontent.com/44728973/106730114-84e35b80-660e-11eb-820a-d04309739443.png)

- You can also add a server name and password.
![Screenshot_20210203_105417](https://user-images.githubusercontent.com/44728973/106730122-86148880-660e-11eb-8b23-d1847085cc0a.png)
![Screenshot_20210203_105453](https://user-images.githubusercontent.com/44728973/106730126-8745b580-660e-11eb-9dd8-8d9d95a5a570.png)

- After you finish configuration, save the file, open terminal and run `./TournamentAssistantCore-linux` again.
![Screenshot_20210203_111845](https://user-images.githubusercontent.com/44728973/106732992-a42fb800-6611-11eb-8479-61404f892eb5.png)

- If the address got verified and port got opened, congratulations, you have just created your own server.
- If the address got verified but port could not be opened, you will need to open the selected port in your router settings manually. 
- If the address failed to verify and port got opened successfully and you made sure that there aren't any spelling mistakes or that the domain is pointed at the correct IP (keep in mind that DNS entries take up to an hour to be created/changed) seek help on discord.

</details>

### TODO
Please help me improve this readme!

## Contributing
Pull requests are welcome! Feel free to dm me on discord if you have any questions or concerns!

## License
[MIT](https://choosealicense.com/licenses/mit/)
