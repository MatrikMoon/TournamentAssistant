import axios from "axios";
import type { SongInfo, SongInfos, Version } from "./songInfo";

export class BeatSaverService {
  private static beatSaverUrl = "https://beatsaver.com";
  private static beatSaverCdnUrl = "https://cdn.beatsaver.com";
  private static beatSaverDownloadByHashUrl = `${this.beatSaverCdnUrl}/`;
  private static beatSaverDownloadByKeyUrl = `${this.beatSaverUrl}/api/download/key/`;
  private static beatSaverGetSongInfoUrl = `${this.beatSaverUrl}/api/maps/id/`;
  private static beatSaverGetSongsInfoUrl = `${this.beatSaverUrl}/api/maps/ids/`;
  private static beatSaverGetSongInfoByHashUrl = `${this.beatSaverUrl}/api/maps/hash/`;

  public static sanitizeSongId(songId: string): string {
    if (songId.startsWith("https://beatsaver.com/") || songId.startsWith("https://bsaber.com/")) {
      // Strip off the trailing slash if there is one
      if (songId.endsWith("/")) {
        songId = songId.slice(0, -1);
      }

      // Strip off the beginning of the URL to leave the ID
      songId = songId.substring(songId.lastIndexOf("/") + 1);
    }

    if (songId.includes('&')) {
      songId = songId.slice(0, songId.indexOf("&"));
    }

    return songId;
  }

  public static async getSongInfo(id: string) {
    const url = `${this.beatSaverGetSongInfoUrl}${id}`;
    return (await axios.get<SongInfo>(url)).data;
  }

  public static async getSongInfos(ids: string[]) {
    const idsEncoded = encodeURIComponent(ids.join(','));
    const url = `${this.beatSaverGetSongsInfoUrl}${idsEncoded}`;
    return (await axios.get<SongInfos>(url)).data;
  }

  public static async getSongInfoByHash(hash: string) {
    const url = `${this.beatSaverGetSongInfoByHashUrl}${hash}`;
    return (await axios.get<SongInfo>(url)).data;
  }

  public static async getSongInfosByHash(hashes: string[]) {
    const hashesEncoded = encodeURIComponent(hashes.join(','));
    const url = `${this.beatSaverGetSongInfoByHashUrl}${hashesEncoded}`;
    return (await axios.get<SongInfos>(url)).data;
  }

  static currentVersion(songInfo: SongInfo): Version | undefined {
    return songInfo.versions.find((x) => x.state === "Published");
  }

  public static characteristics(songInfo: SongInfo): string[] {
    return [...new Set(BeatSaverService.currentVersion(songInfo)?.diffs.map((x) => x.characteristic))];
  }

  public static getMaxScore(
    songInfo: SongInfo,
    characteristic: string,
    difficulty: string
  ): number {
    const diff = BeatSaverService.currentVersion(songInfo)?.diffs.find(
      (x) => x.characteristic.toLowerCase() === characteristic.toLowerCase() && x.difficulty.toLowerCase() === difficulty.toLowerCase()
    );

    return diff?.maxScore ?? 0;
  }

  public static getClosestDifficultyPreferLower(
    songInfo: SongInfo,
    characteristic: string,
    difficulty: string
  ): string | undefined {
    if (BeatSaverService.hasDifficulty(songInfo, characteristic, difficulty)) return difficulty;

    const lowerDifficulty = BeatSaverService.getLowerDifficulty(
      songInfo,
      characteristic,
      difficulty
    );
    if (lowerDifficulty === undefined) {
      return BeatSaverService.getHigherDifficulty(songInfo, characteristic, difficulty);
    }
    return lowerDifficulty;
  }

  static getLowerDifficulty(
    songInfo: SongInfo,
    characteristic: string,
    difficulty: string
  ): string | undefined {
    const difficulties = BeatSaverService.getDifficultiesAsArray(songInfo, characteristic);
    const lowerDifficulties = difficulties.filter((x) => x < difficulty);
    return lowerDifficulties[lowerDifficulties.length - 1];
  }

  static getHigherDifficulty(
    songInfo: SongInfo,
    characteristic: string,
    difficulty: string
  ): string | undefined {
    const difficulties = BeatSaverService.getDifficultiesAsArray(songInfo, characteristic);
    const higherDifficulties = difficulties.filter((x) => x > difficulty);
    return higherDifficulties[0];
  }

  public static getDifficultiesAsArray(
    songInfo: SongInfo,
    characteristic: string
  ): string[] {
    const characteristicDiffs = BeatSaverService.currentVersion(songInfo)?.diffs.filter(
      (x) => x.characteristic.toLowerCase() === characteristic.toLowerCase()
    );
    if (characteristicDiffs && characteristicDiffs.length > 0) {
      return characteristicDiffs
        .map((x) => x.difficulty)
        .sort((a, b) => this.getDifficultyAsNumber(a) - this.getDifficultyAsNumber(b));
    }
    return [];
  }

  public static hasDifficulty(
    songInfo: SongInfo,
    characteristic: string,
    difficulty: string
  ): boolean {
    const characteristicDiffs = BeatSaverService.currentVersion(songInfo)?.diffs.filter(
      (x) =>
        x.characteristic.toLowerCase() === characteristic.toLowerCase() &&
        x.difficulty.toLowerCase() === difficulty.toLowerCase()
    );
    return characteristicDiffs !== undefined && characteristicDiffs.length > 0;
  }

  public static getDifficultyAsNumber(difficulty: string) {
    switch (difficulty) {
      case "Easy":
        return 0;
      case "Normal":
        return 1;
      case "Hard":
        return 2;
      case "Expert":
        return 3;
      case "ExpertPlus":
        return 4;
      default:
        return -1;
    }
  }

  public static getDifficultyAsString(difficulty: number) {
    switch (difficulty) {
      case 0:
        return "Easy";
      case 1:
        return "Normal";
      case 2:
        return "Hard";
      case 3:
        return "Expert";
      case 4:
        return "ExpertPlus";
      default:
        return "NotFound";
    }
  }
}
