import axios from "axios";
import type { SongInfo, Version } from "./songInfo";

export class BeatSaverService {
  private static beatSaverUrl = "https://beatsaver.com";
  private static beatSaverCdnUrl = "https://cdn.beatsaver.com";
  private static beatSaverDownloadByHashUrl = `${this.beatSaverCdnUrl}/`;
  private static beatSaverDownloadByKeyUrl = `${this.beatSaverUrl}/api/download/key/`;
  private static beatSaverGetSongInfoUrl = `${this.beatSaverUrl}/api/maps/id/`;
  private static beatSaverGetSongInfoByHashUrl = `${this.beatSaverUrl}/api/maps/hash/`;

  public static async getSongInfo(id: string) {
    const url = `${this.beatSaverGetSongInfoUrl}${id}`;
    return (await axios.get<SongInfo>(url)).data;
  }

  public static async getSongInfoByHash(hash: string) {
    const url = `${this.beatSaverGetSongInfoByHashUrl}${hash}`;
    return (await axios.get<SongInfo>(url)).data;
  }

  static currentVersion(songInfo: SongInfo): Version | undefined {
    return songInfo.versions.find((x) => x.state === "Published");
  }

  public static characteristics(songInfo: SongInfo): string[] {
    return [...new Set(BeatSaverService.currentVersion(songInfo)?.diffs.map((x) => x.characteristic))] || [];
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
        .sort((a, b) => a.localeCompare(b));
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
}
