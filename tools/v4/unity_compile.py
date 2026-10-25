"""Compile the Unity C# assemblies without Unity (the editor may hold the project, and Temp/ belongs to it).

Copies the Unity-generated *.csproj from the repo root into a scratch folder, makes every path absolute, moves obj/bin
into the scratch folder and rebuilds each project's <Compile> list from its asmdef folder (so files added since Unity
last generated the projects are compiled too). Then `dotnet build` the requested project (default: the PlayMode test
assembly, which pulls in the whole runtime).

    python tools/v4/unity_compile.py [--project Galaxy101.PlayMode.Tests] [--scratch <dir>] [--repo <repo root>]
"""
import argparse, json, os, re, subprocess, sys, tempfile

SKIP_DIRS = {"Library", "Temp", "obj", "bin", "Logs", "artifacts", "Design", "Server", "Shared", "node_modules", ".git"}


def asmdef_dirs(repo):
    found = {}
    for top in ("Assets", "Packages"):
        for root, dirs, files in os.walk(os.path.join(repo, top)):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for f in files:
                if f.endswith(".asmdef"):
                    with open(os.path.join(root, f), encoding="utf-8-sig") as h:
                        found[json.load(h)["name"]] = root
    return found


def sources(folder, owned):
    out = []
    for root, dirs, files in os.walk(folder):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and not (os.path.join(root, d) in owned and os.path.join(root, d) != folder)]
        for f in files:
            if f.endswith(".cs"): out.append(os.path.join(root, f))
    return sorted(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
    ap.add_argument("--project", default="Galaxy101.PlayMode.Tests")
    ap.add_argument("--scratch", default=os.path.join(tempfile.gettempdir(), "g101-unity-compile"))
    a = ap.parse_args()
    repo, scratch = os.path.abspath(a.repo), os.path.abspath(a.scratch)
    os.makedirs(scratch, exist_ok=True)
    asm = asmdef_dirs(repo)
    owned = set(asm.values())
    for f in os.listdir(repo):
        if not f.endswith(".csproj"): continue
        name = f[:-7]
        with open(os.path.join(repo, f), encoding="utf-8-sig") as h: text = h.read()
        text = text.replace("<BaseIntermediateOutputPath>Temp\\obj\\$(MSBuildProjectName)</BaseIntermediateOutputPath>",
                            f"<BaseIntermediateOutputPath>{scratch}\\obj\\$(MSBuildProjectName)\\</BaseIntermediateOutputPath>")
        text = text.replace("<OutputPath>Temp\\bin\\Debug\\</OutputPath>", f"<OutputPath>{scratch}\\bin\\</OutputPath>")
        def absolute(m):
            path = m.group(2)
            if re.match(r"^[A-Za-z]:\\", path) or path.endswith(".csproj"): return m.group(0)
            return f'{m.group(1)}"{os.path.join(repo, path)}"'
        text = re.sub(r'(Include=)"([^"]+)"', absolute, text)
        text = re.sub(r"<HintPath>(?![A-Za-z]:\\)([^<]+)</HintPath>", lambda m: f"<HintPath>{os.path.join(repo, m.group(1))}</HintPath>", text)
        if name in asm:
            files = sources(asm[name], owned)
            items = "\n".join(f'    <Compile Include="{p}" />' for p in files)
            text = re.sub(r"\s*<Compile Include=\"[^\"]+\" />", "", text)
            text = text.replace("</Project>", f"  <ItemGroup>\n{items}\n  </ItemGroup>\n</Project>", 1) if "</Project>" in text else text
        with open(os.path.join(scratch, f), "w", encoding="utf-8") as h: h.write(text)
    proj = os.path.join(scratch, a.project + ".csproj")
    r = subprocess.run(["dotnet", "build", proj, "-nologo", "-v:q", "-clp:ErrorsOnly"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (r.stdout + r.stderr).strip()
    errors = sorted(set(l.strip() for l in out.splitlines() if ": error " in l))
    for l in errors: print(l)
    print(f"{a.project}: {'OK' if r.returncode == 0 else 'FAILED'} ({len(errors)} errors)")
    return r.returncode


if __name__ == "__main__":
    sys.exit(main())
