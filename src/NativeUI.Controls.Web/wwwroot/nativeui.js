import { dotnet } from "../../_framework/dotnet.js";
import { dom } from "./nativeui-dom.js";
import { canvas } from "./nativeui-canvas.js";
import { image } from "./nativeui-image.js";

const runtime = await dotnet.create();
runtime.setModuleImports("nativeui-dom", dom);
runtime.setModuleImports("nativeui-canvas", canvas);
runtime.setModuleImports("nativeui-image", image);
await dotnet.run();
