import jwt_decode from "jwt-decode";

export function getAvatarFromToken(token: string) {
  const userId = jwt_decode<any>(token)["ta:discord_id"];
  const avatarId = jwt_decode<any>(token)["ta:discord_avatar"];
  return `https://cdn.discordapp.com/avatars/${userId}/${avatarId}.png`;
}

export function getUsernameFromToken(token: string) {
  return jwt_decode<any>(token)["ta:discord_name"];
}

export function getUserIdFromToken(token: string) {
  return jwt_decode<any>(token)["ta:discord_id"];
}
