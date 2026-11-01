import { dotnet } from "../../_framework/dotnet.js";
import { dom } from "./nativeui-dom.js";
import { canvas } from "./nativeui-canvas.js";

const runtime = await dotnet.create();
runtime.setModuleImports("nativeui-dom", dom);
runtime.setModuleImports("nativeui-canvas", canvas);
await dotnet.run();
