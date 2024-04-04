import type { Playlist } from "./playlist";

export class PlaylistService {

    public static async loadPlaylist(file: File): Promise<Playlist> {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();

            reader.onload = (event) => {
                const text = event.target?.result;
                try {
                    resolve(JSON.parse(text as string) as Playlist);
                } catch (error) {
                    reject("Failed to parse JSON: " + error);
                }
            };

            reader.onerror = () => {
                reject("Failed to read file: " + reader.error);
            };

            reader.readAsText(file);
        });
    }
}
