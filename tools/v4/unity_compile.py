"""Compile the Unity C# assemblies without Unity (the editor may hold the project, and Temp/ belongs to it).

Copies the Unity-generated *.csproj from the repo root into a scratch folder, makes every path absolute, moves obj/bin
into the scratch folder and rebuilds each project's <Compile> list from its asmdef folder (so files added since Unity
last generated the projects are compiled too). An asmdef Unity has not generated a project for yet (a new package or
assembly in a worktree) gets one made from a generated project of the same kind (pure / engine / tests: netstandard2.1,
LangVersion 9 and the references Unity gives that kind), and every project gets the project references its asmdef names
but the generated file lacks. Then `dotnet build` the requested project (default: the PlayMode test assembly, which pulls
in the whole runtime).

    python tools/v4/unity_compile.py [--project Galaxy101.PlayMode.Tests] [--scratch <dir>] [--repo <repo root>]
"""
import argparse, json, os, re, subprocess, sys, tempfile

SKIP_DIRS = {"Library", "Temp", "obj", "bin", "Logs", "artifacts", "Design", "Server", "Shared", "node_modules", ".git"}


def asmdef_dirs(repo):
    found, defs = {}, {}
    for top in ("Assets", "Packages"):
        for root, dirs, files in os.walk(os.path.join(repo, top)):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for f in files:
                if f.endswith(".asmdef"):
                    with open(os.path.join(root, f), encoding="utf-8-sig") as h:
                        d = json.load(h)
                    found[d["name"]] = root
                    defs[d["name"]] = d
    return found, defs


def is_test(d):
    return "TestAssemblies" in (d.get("optionalUnityReferences") or []) or "UNITY_INCLUDE_TESTS" in (d.get("defineConstraints") or [])


def template_for(d, generated, defs):
    """A generated project of the same kind as asmdef d: tests, pure (noEngineReferences) or engine."""
    want = "test" if is_test(d) else "pure" if d.get("noEngineReferences") else "engine"
    best, score = None, -1
    for name in sorted(generated):
        o = defs.get(name)
        if o is None: continue
        kind = "test" if is_test(o) else "pure" if o.get("noEngineReferences") else "engine"
        if kind != want or (want == "test" and bool(o.get("includePlatforms")) != bool(d.get("includePlatforms"))): continue
        # Prefer the same reference rules: overrideReferences and the precompiled assemblies it lists (nunit, Newtonsoft).
        s = (2 if bool(o.get("overrideReferences")) == bool(d.get("overrideReferences")) else 0) + len(set(o.get("precompiledReferences") or []) & set(d.get("precompiledReferences") or []))
        if s > score: best, score = name, s
    return best


def with_references(text, names):
    """Adds <ProjectReference> items for names the project does not reference yet."""
    have = set(re.findall(r'<ProjectReference Include="([^"]+)\.csproj"', text))
    missing = [n for n in names if n not in have]
    if not missing: return text
    items = "\n".join(f'    <ProjectReference Include="{n}.csproj" />' for n in missing)
    return text.replace('  <Import Project="Sdk.targets"', f'  <ItemGroup>\n{items}\n  </ItemGroup>\n  <Import Project="Sdk.targets"', 1)


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
    ap.add_argument("--csproj-from", help="folder holding the Unity-generated *.csproj (default: --repo); lets a git worktree, which has none, compile with the main copy's")
    a = ap.parse_args()
    repo, scratch = os.path.abspath(a.repo), os.path.abspath(a.scratch)
    os.makedirs(scratch, exist_ok=True)
    asm, defs = asmdef_dirs(repo)
    owned = set(asm.values())
    gen = os.path.abspath(a.csproj_from or repo)
    generated = {f[:-7] for f in os.listdir(gen) if f.endswith(".csproj")}
    jobs = [(name, name) for name in sorted(generated)]
    for name in sorted(asm):
        if name in generated: continue
        base = template_for(defs[name], generated, defs)
        if base is None: print(f"no template project for {name}"); continue
        jobs.append((name, base))
    for name, base in jobs:
        with open(os.path.join(gen, base + ".csproj"), encoding="utf-8-sig") as h: text = h.read()
        if name != base:
            text = text.replace(f"<RootNamespace>{defs[base].get('rootNamespace') or base}</RootNamespace>", f"<RootNamespace>{defs[name].get('rootNamespace') or name}</RootNamespace>")
            text = text.replace(f"<AssemblyName>{base}</AssemblyName>", f"<AssemblyName>{name}</AssemblyName>")
            text = re.sub(r"\s*<ProjectReference Include=\"[^\"]+\" />", "", text)
        text = text.replace("<BaseIntermediateOutputPath>Temp\\obj\\$(MSBuildProjectName)</BaseIntermediateOutputPath>",
                            f"<BaseIntermediateOutputPath>{scratch}\\obj\\$(MSBuildProjectName)\\</BaseIntermediateOutputPath>")
        text = text.replace("<OutputPath>Temp\\bin\\Debug\\</OutputPath>", f"<OutputPath>{scratch}\\bin\\</OutputPath>")
        def absolute(m):
            path = m.group(2)
            if re.match(r"^[A-Za-z]:\\", path) or path.endswith(".csproj"): return m.group(0)
            return f'{m.group(1)}"{os.path.join(gen if path.startswith("Library") else repo, path)}"'
        text = re.sub(r'(Include=)"([^"]+)"', absolute, text)
        text = re.sub(r"<HintPath>(?![A-Za-z]:\\)([^<]+)</HintPath>", lambda m: f"<HintPath>{os.path.join(gen if m.group(1).startswith('Library') else repo, m.group(1))}</HintPath>", text)
        if name in asm:
            files = sources(asm[name], owned)
            items = "\n".join(f'    <Compile Include="{p}" />' for p in files)
            text = re.sub(r"\s*<Compile Include=\"[^\"]+\" />", "", text)
            text = text.replace("</Project>", f"  <ItemGroup>\n{items}\n  </ItemGroup>\n</Project>", 1) if "</Project>" in text else text
            refs = [r for r in (defs[name].get("references") or []) if not r.startswith("GUID:") and r in asm]
            text = with_references(text, refs)
        with open(os.path.join(scratch, name + ".csproj"), "w", encoding="utf-8") as h: h.write(text)
    proj = os.path.join(scratch, a.project + ".csproj")
    r = subprocess.run(["dotnet", "build", proj, "-nologo", "-v:q", "-clp:ErrorsOnly"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (r.stdout + r.stderr).strip()
    errors = sorted(set(l.strip() for l in out.splitlines() if ": error " in l))
    for l in errors: print(l)
    print(f"{a.project}: {'OK' if r.returncode == 0 else 'FAILED'} ({len(errors)} errors)")
    return r.returncode


if __name__ == "__main__":
    sys.exit(main())
