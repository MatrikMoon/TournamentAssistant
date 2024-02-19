import type { QualifierEvent_QualifierMap } from "tournament-assistant-client";
import type { SongInfo } from "./services/beatSaver/songInfo";

export interface QualifierMapWithSongInfo extends QualifierEvent_QualifierMap {
    songInfo: SongInfo;
}