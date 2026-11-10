"""Read-only artifact checks; does not install packages or delete project files."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import tarfile
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--legacy-unitypackage', type=Path, required=True)
parser.add_argument('--vpm-zip', type=Path, required=True)
parser.add_argument('--listing-index', type=Path, required=True)
parser.add_argument('--installer-unitypackage', type=Path, required=True)
args = parser.parse_args()
root = 'Assets/LumaKroma/SPS2SetupAssistant'
guid = '1824306e4bd4439aaffbd9eab24486e5'
expected = {root: guid}
package_id = 'com.lumakroma.sps2-setup-assistant'

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def unitypackage(path):
    entries = {}
    with tarfile.open(path) as archive:
        names = archive.getnames()
        require(len(names) == len(set(names)), 'Duplicate archive paths')
        for item in archive:
            if not item.name.endswith('/pathname'):
                continue
            prefix = item.name.rsplit('/', 1)[0]
            name = archive.extractfile(item).read().decode().rstrip('\0')
            require(name not in entries, 'Duplicate Unity asset paths')
            meta = archive.extractfile(prefix + '/asset.meta').read()
            data = archive.extractfile(prefix + '/asset').read() if prefix + '/asset' in names else None
            entries[name] = (meta, data)
    return entries

old = unitypackage(args.legacy_unitypackage)
require(root in old, 'Published legacy root missing')
require(re.search(rb'^guid: ([0-9a-f]{32})$', old[root][0], re.M).group(1).decode() == guid,
        'Metadata GUID differs from published legacy folder')
require(all(name == root or name.startswith(root + '/') for name in old), 'Legacy archive escapes exact root')
with zipfile.ZipFile(args.vpm_zip) as archive:
    manifest = json.loads(archive.read('package.json'))
    require(manifest['name'] == package_id and manifest['version'] == '1.1.0', 'Unexpected candidate')
    require(manifest.get('legacyFolders') == expected, 'ZIP migration target mismatch')
    require('legacyFiles' not in manifest and 'legacyPackages' not in manifest, 'Additional deletion scope')
    shared = 0
    for oldpath, (oldmeta, _) in old.items():
        if oldpath == root:
            continue
        relative = oldpath[len(root) + 1:] + '.meta'
        if relative not in archive.namelist():
            require(relative == 'UNITYPACKAGE.md.meta', 'Unexpected lost legacy asset: ' + relative)
            continue
        oldguid = re.search(rb'^guid: ([0-9a-f]{32})$', oldmeta, re.M).group(1)
        newguid = re.search(rb'^guid: ([0-9a-f]{32})$', archive.read(relative), re.M).group(1)
        require(oldguid == newguid, 'Shared asset GUID changed: ' + relative)
        shared += 1
require(shared == 40, 'Published legacy shared-asset inventory changed')
index = json.loads(args.listing_index.read_text(encoding='utf-8-sig'))
listed = index['packages'][package_id]['versions']['1.1.0']
require(listed.get('legacyFolders') == expected, 'Actual listing builder lost metadata')
require('legacyFiles' not in listed and 'legacyPackages' not in listed, 'Listing expands deletion scope')
require(listed['zipSHA256'] == hashlib.sha256(args.vpm_zip.read_bytes()).hexdigest(), 'Listing ZIP mismatch')
installer = unitypackage(args.installer_unitypackage)
require(len(installer) == 3, 'Official installer layout changed')
config_path = 'Assets/com.anatawa12.vpm-package-auto-installer/config.json'
config = json.loads(installer[config_path][1])
require(config['vpmDependencies'][package_id] == '1.1.0', 'Installer version mismatch')
require(config['silentIfInstalled'] is False, 'Unexpected silent-if-installed policy')
require('legacyFolders' not in config and 'legacyFiles' not in config, 'Metadata placed in installer config')
print(json.dumps({'artifactChecks': 'PASS', 'legacyRoot': root, 'rootGuid': guid,
                  'sharedGuidsPreserved': shared, 'metadataReachedRealZipAndListing': True,
                  'nativeMigrationExecuted': False, 'deletedFiles': 0}, indent=2))
