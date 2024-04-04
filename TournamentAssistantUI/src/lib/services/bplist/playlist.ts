export interface Playlist {
    playlistTitle: string;
    playlistAuthor: string;
    playlistDescription: string;
    image: string;
    songs: Song[];
}

export interface Song {
    key: string;
    hash: string;
    songName: string;
}