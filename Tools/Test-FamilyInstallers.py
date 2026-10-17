"""Audit family installer payloads without importing them into a Unity project."""
import argparse, hashlib, json, pathlib, tarfile, zipfile

parser = argparse.ArgumentParser()
parser.add_argument('output', type=pathlib.Path)
parser.add_argument('template', type=pathlib.Path)
args = parser.parse_args()
root = pathlib.Path(__file__).resolve().parent.parent
distribution = root / 'Distribution/1.2.0-beta.3'

def read_package(path):
    with tarfile.open(path, 'r:gz') as archive:
        members = archive.getmembers()
        assert all(m.isfile() or m.isdir() for m in members), 'Unexpected tar member type'
        assert len({m.name for m in members}) == len(members), 'Duplicate member'
        data = {m.name.removeprefix('./'): archive.extractfile(m).read() for m in members if m.isfile()}
    paths = {key.rsplit('/', 1)[0]: value.decode().strip() for key, value in data.items() if key.endswith('/pathname')}
    return data, paths

template, template_paths = read_package(args.template)
assert len(template_paths) == 3, 'Expected official three-asset template'
result = []
for family in ('Fury', 'NDMF'):
    name = f'SPS2-Setup-Assistant-1.2.0-beta.3-{family}-Installer'
    package = args.output / f'{name}.unitypackage'
    data, paths = read_package(package)
    assert paths == template_paths, 'Unexpected payload or GUID change'
    assert set(data) == set(template), 'Unexpected tar entry'
    config_guid = next(g for g, p in paths.items() if p.endswith('/config.json'))
    config_key = config_guid + '/asset'
    for key in data:
        if key != config_key:
            assert data[key] == template[key], f'Installer source changed: {key}'
    config = json.loads(data[config_key])
    expected = json.loads((distribution / f'{family}-config.json').read_text(encoding='utf-8'))
    assert config == expected, 'Config mismatch'
    assert config['includePrerelease'] is True and config['silentIfInstalled'] is False
    assert config['minimumUnity'] == '2022.3'
    deps = config['vpmDependencies']
    assert deps['com.lumakroma.sps2-setup-assistant'] == '1.2.0-beta.3'
    assert ('com.vrcfury.vrcfury' in deps) == (family == 'Fury')
    assert ('net.nrzk.spsndmf' in deps) == (family == 'NDMF')
    assert 'net.nrzk.vfstub' not in deps and 'net.nrzk.spsndmf-migrator' not in deps
    customer_zip = args.output / f'{name}.zip'
    with zipfile.ZipFile(customer_zip) as archive:
        assert set(archive.namelist()) == {package.name, 'README.txt', 'VPAI-LICENSE.txt', 'VPAI-LICENSE-semver.net.txt'}
        assert archive.read(package.name) == package.read_bytes()
        for filename in ('README.txt', 'VPAI-LICENSE.txt', 'VPAI-LICENSE-semver.net.txt'):
            assert archive.read(filename) == (distribution / filename).read_bytes()
    result.append(dict(family=family, paths=paths, config=config,
        unitypackageSHA256=hashlib.sha256(package.read_bytes()).hexdigest(),
        zipSHA256=hashlib.sha256(customer_zip.read_bytes()).hexdigest()))
print(json.dumps(dict(status='PASS', officialTemplatePayloadUnchangedExceptConfig=True, installers=result), indent=2))
