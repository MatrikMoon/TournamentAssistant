import type { Map } from "tournament-assistant-client";
import type { SongInfo } from "./services/beatSaver/songInfo";

export interface MapWithSongInfo extends Map {
    songInfo: SongInfo;
}