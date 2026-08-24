import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";

import { baseConfig } from "./base.js";

/** Rules for shared React packages that are not Next.js apps. */
export const reactLibraryConfig = [
  ...baseConfig,
  {
    files: ["**/*.{ts,tsx}"],
    plugins: {
      "react-hooks": reactHooks,
    },
    languageOptions: {
      globals: {
        ...globals.browser,
      },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
    },
  },
];

export default reactLibraryConfig;
