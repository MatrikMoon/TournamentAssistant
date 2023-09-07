import axios from "axios";
import type { SongInfo } from "./songInfo";

export class BeatSaverService {
  private static beatSaverUrl = "https://beatsaver.com";
  private static beatSaverCdnUrl = "https://cdn.beatsaver.com";
  private static beatSaverDownloadByHashUrl = `${this.beatSaverCdnUrl}/`;
  private static beatSaverDownloadByKeyUrl = `${this.beatSaverUrl}/api/download/key/`;
  private static beatSaverGetSongInfoUrl = `${this.beatSaverUrl}/api/maps/id/`;

  public static async getSongInfo(id: string) {
    const url = `${this.beatSaverGetSongInfoUrl}${id}`;
    return (await axios.get<SongInfo>(url)).data;
  }
}
