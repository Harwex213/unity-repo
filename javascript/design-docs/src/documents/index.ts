import { biomes } from "./biomes";
import { ostrovCoreCycle } from "./ostrov-core-cycle";
import type { TDocument } from "../core/types";

// The list of documents in the sidebar, in this order.
// A new document is a folder next to this file, listed here.
const DOCUMENTS: TDocument[] = [ostrovCoreCycle, biomes];

export { DOCUMENTS };
