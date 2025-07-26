import { masterAddress, masterApiPort } from "tournament-assistant-client";
import { idbGet, idbSet } from "./indexedDbUtils";

export function getSelectedEnumMembers<T extends Record<keyof T, number>>(
  enumType: T,
  value: number
): Extract<keyof T, string>[] {
  function hasFlag(value: number, flag: number): boolean {
    return (value & flag) === flag;
  }

  const selectedMembers: Extract<keyof T, string>[] = [];
  for (const member in enumType) {
    if (hasFlag(value, enumType[member])) {
      selectedMembers.push(member);
    }
  }
  return selectedMembers;
}

export function getBadgeTextFromDifficulty(difficulty: number) {
  switch (difficulty) {
    case 1:
      return "Normal";
    case 2:
      return "Hard";
    case 3:
      return "Expert";
    case 4:
      return "Expert+";
    default:
      return "Easy";
  }
}

export async function fetchImageAsUint8Array(
  url: string,
  fromMasterServer = true
): Promise<Uint8Array> {
  if (fromMasterServer) {
    url = `https://${masterAddress}:${masterApiPort}/api/file/${url}`;
  }

  // 1. Try IndexedDB
  const cached = await idbGet(url);
  if (cached) return cached;

  // 2. Fetch if not found
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(
      `Failed to fetch image: ${response.status} ${response.statusText}`
    );
  }

  const buffer = await response.arrayBuffer();
  const data = new Uint8Array(buffer);

  // 3. Save to IndexedDB
  await idbSet(url, data);

  return data;
}
