/** One decimal, no trailing zero: 3, 2.5. */
const formatAverage = (value: number) => {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
};

export { formatAverage };
