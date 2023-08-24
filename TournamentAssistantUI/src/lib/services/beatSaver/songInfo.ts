export interface Uploader {
  id: number;
  name: string;
  uniqueSet: boolean;
  hash: string;
  avatar: string;
  type: string;
}

export interface Metadata {
  bpm: number;
  duration: number;
  songName: string;
  songSubName: string;
  songAuthorName: string;
  levelAuthorName: string;
}

export interface Stats {
  plays: number;
  downloads: number;
  upvotes: number;
  downvotes: number;
  score: number;
}

export interface ParitySummary {
  errors: number;
  warns: number;
  resets: number;
}

export interface Diff {
  njs: number;
  offset: number;
  notes: number;
  bombs: number;
  obstacles: number;
  nps: number;
  length: number;
  characteristic: string;
  difficulty: string;
  events: number;
  chroma: boolean;
  me: boolean;
  ne: boolean;
  cinema: boolean;
  seconds: number;
  paritySummary: ParitySummary;
}

export interface Version {
  hash: string;
  state: string;
  createdAt: Date;
  sageScore: number;
  diffs: Diff[];
  downloadURL: string;
  coverURL: string;
  previewURL: string;
}

export interface SongInfo {
  id: string;
  name: string;
  description: string;
  uploader: Uploader;
  metadata: Metadata;
  stats: Stats;
  uploaded: Date;
  automapper: boolean;
  ranked: boolean;
  qualified: boolean;
  versions: Version[];
  createdAt: Date;
  updatedAt: Date;
  lastPublishedAt: Date;
}

function currentVersion(songInfo: SongInfo): Version | undefined {
  return songInfo.versions.find((x) => x.state === "Published");
}

export function characteristics(songInfo: SongInfo): string[] {
  return currentVersion(songInfo)?.diffs.map((x) => x.characteristic) || [];
}

export function getClosestDifficultyPreferLower(
  songInfo: SongInfo,
  characteristic: string,
  difficulty: string
): string | undefined {
  if (hasDifficulty(songInfo, characteristic, difficulty)) return difficulty;

  const lowerDifficulty = getLowerDifficulty(
    songInfo,
    characteristic,
    difficulty
  );
  if (lowerDifficulty === undefined) {
    return getHigherDifficulty(songInfo, characteristic, difficulty);
  }
  return lowerDifficulty;
}

function getLowerDifficulty(
  songInfo: SongInfo,
  characteristic: string,
  difficulty: string
): string | undefined {
  const difficulties = getDifficultiesAsArray(songInfo, characteristic);
  const lowerDifficulties = difficulties.filter((x) => x < difficulty);
  return lowerDifficulties[lowerDifficulties.length - 1];
}

function getHigherDifficulty(
  songInfo: SongInfo,
  characteristic: string,
  difficulty: string
): string | undefined {
  const difficulties = getDifficultiesAsArray(songInfo, characteristic);
  const higherDifficulties = difficulties.filter((x) => x > difficulty);
  return higherDifficulties[0];
}

function getDifficultiesAsArray(
  songInfo: SongInfo,
  characteristic: string
): string[] {
  const characteristicDiffs = currentVersion(songInfo)?.diffs.filter(
    (x) => x.characteristic.toLowerCase() === characteristic.toLowerCase()
  );
  if (characteristicDiffs && characteristicDiffs.length > 0) {
    return characteristicDiffs
      .map((x) => x.difficulty)
      .sort((a, b) => a.localeCompare(b));
  }
  return [];
}

export function hasDifficulty(
  songInfo: SongInfo,
  characteristic: string,
  difficulty: string
): boolean {
  const characteristicDiffs = currentVersion(songInfo)?.diffs.filter(
    (x) =>
      x.characteristic.toLowerCase() === characteristic.toLowerCase() &&
      x.difficulty.toLowerCase() === difficulty.toLowerCase()
  );
  return characteristicDiffs !== undefined && characteristicDiffs.length > 0;
}
