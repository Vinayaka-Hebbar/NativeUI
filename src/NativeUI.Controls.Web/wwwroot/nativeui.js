import { dotnet } from "../../_framework/dotnet.js";
import { nativeui } from "./nativeui-dom.js";

const runtime = await dotnet.create();
runtime.setModuleImports("nativeui-dom", { nativeui });
await dotnet.run();
