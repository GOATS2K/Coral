import {
  generateSchemaTypes,
  generateReactQueryComponents,
} from "@openapi-codegen/typescript";
import { defineConfig } from "@openapi-codegen/cli";
export default defineConfig({
  client: {
    from: {
      relativePath: "../Coral.Api/openapi.json",
      source: "file",
    },
    outputDir: "lib/client",
    to: async (context) => {
      const filenamePrefix = "";
      const { schemasFiles } = await generateSchemaTypes(context, {
        filenamePrefix,
      });
      await generateReactQueryComponents(context, {
        filenamePrefix,
        schemasFiles,
      });
    },
  },
});
