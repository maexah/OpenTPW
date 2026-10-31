#!/bin/bash
# vkvalidate.sh [DIR] - makes a Vulkan layer folder holding the Khronos validation layer and prints the environment that turns it on.
#   eval "$(vkvalidate.sh)"            then launch the game (gamedrive.py passes the environment on); the layer's messages land in the game's
#                                      stdout, so a harness's run.log: grep VUID run.log
# The layer's library comes from OPENTPW_VK_VALIDATION_SO (a libVkLayer_khronos_validation.so; the Steam runtime and the Flatpak GL extension
# each carry one). DIR defaults to $OPENTPW_HARNESS_WORK/vklayer. The manifest's library_path is absolute: the loader does not look for a
# bare file name beside the manifest, and says "cannot open shared object file" (Q957).
set -e
SO=${OPENTPW_VK_VALIDATION_SO:?set OPENTPW_VK_VALIDATION_SO to a libVkLayer_khronos_validation.so}
DIR=${1:-${OPENTPW_HARNESS_WORK:?set OPENTPW_HARNESS_WORK or give a folder}/vklayer}
mkdir -p "$DIR"; SO=$(readlink -f "$SO"); cp -f "$SO" "$DIR/libVkLayer_khronos_validation.so"
cat > "$DIR/VkLayer_khronos_validation.json" <<JSON
{"file_format_version":"1.2.0","layer":{"name":"VK_LAYER_KHRONOS_validation","type":"GLOBAL","library_path":"$DIR/libVkLayer_khronos_validation.so","api_version":"1.3.290","implementation_version":"1","description":"Khronos Validation Layer"}}
JSON
echo "export VK_LAYER_PATH=$DIR VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation"
