const SEMVER_PATTERN = /^(\d+)\.(\d+)\.(\d+)$/;

const parseSemver = (version: string): [number, number, number] | null => {
  const match = SEMVER_PATTERN.exec(version);
  if (!match) {
    return null;
  }

  return [Number(match[1]), Number(match[2]), Number(match[3])];
};

// The result is negative when a is older than b.
// A string that is not semver goes after every valid version.
const compareSemver = (a: string, b: string): number => {
  const left = parseSemver(a);
  const right = parseSemver(b);
  if (!left || !right) {
    if (left) {
      return -1;
    }
    if (right) {
      return 1;
    }
    return a.localeCompare(b);
  }

  for (let i = 0; i < 3; i++) {
    const diff = (left[i] ?? 0) - (right[i] ?? 0);
    if (diff !== 0) {
      return diff;
    }
  }

  return 0;
};

export { compareSemver, parseSemver };
