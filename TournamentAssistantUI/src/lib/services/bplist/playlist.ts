export interface Playlist {
    playlistTitle: string;
    playlistAuthor: string;
    playlistDescription: string;
    image: string;
    songs: Song[];
}

export interface Song {
    hash: string;
    songName: string;
}