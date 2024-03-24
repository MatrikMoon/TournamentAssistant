export function getSelectedEnumMembers<T extends Record<keyof T, number>>(
    enumType: T,
    value: number,
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