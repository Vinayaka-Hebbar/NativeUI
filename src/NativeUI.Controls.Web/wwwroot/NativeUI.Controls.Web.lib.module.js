export function onRuntimeConfigLoaded(config) {
    if (config.applicationEnvironment !== "Development") {
        return;
    }

    // Fingerprinted development assets can retain cached failures across rebuilds.
    for (const resources of Object.values(config.resources)) {
        if (Array.isArray(resources)) {
            disableCaching(resources);
        }
    }

    for (const resources of Object.values(config.resources.satelliteResources ?? {})) {
        disableCaching(resources);
    }
}

function disableCaching(resources) {
    for (const resource of resources) {
        resource.cache = "no-store";
    }
}
