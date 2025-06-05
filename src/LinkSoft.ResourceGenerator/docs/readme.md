# Resource Generator for .NET Projects

## Overview

This project replaces the T4 templates used for resource generation with a modern, cross-platform Roslyn Source Generator. This approach offers several benefits:

## Benefits of Using Source Generators

1. **Cross-Platform Support**: Works on Windows, macOS, and Linux without any special dependencies
2. **Build Integration**: Automatically runs as part of the standard build process
3. **No Visual Studio Dependency**: Removes all Visual Studio-specific dependencies (EnvDTE, etc.)
4. **Performance**: Source generators are generally faster than T4 templates
5. **Better IDE Support**: Full IntelliSense support for the generated code
6. **Simpler Maintenance**: Common code in one place rather than duplicated T4 templates

## Affected Projects

The following projects have been migrated from T4 templates to source generators:

- `CustomerPortal.Resources`
- `BackOffice.Resources`
- `LeadManagement.Resources`

## Steps

1. First, build the ResourceGenerator project:
```bash
dotnet build src/ResourceGenerator/ResourceGenerator.csproj
```

2. For each project containing a Resources.tt file (BackOffice.Resources, CustomerPortal.Resources, LeadManagement.Resources), update the project file:

   a. Open the .csproj file
   b. Remove these items:
   ```xml
   <ItemGroup>
     <None Update="Resources.tt">
       <Generator>TextTemplatingFileGenerator</Generator>
       <LastGenOutput>Resources.cs</LastGenOutput>
     </None>
   </ItemGroup>

   <ItemGroup>
     <Service Include="{508349b6-6b84-4df5-91f0-309beebad82d}" />
   </ItemGroup>

   <ItemGroup>
     <Compile Update="Resources.cs">
       <DesignTime>True</DesignTime>
       <AutoGen>True</AutoGen>
       <DependentUpon>Resources.tt</DependentUpon>
     </Compile>
   </ItemGroup>
   ```

   c. Add these items instead:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\..\ResourceGenerator\ResourceGenerator.csproj" 
                       OutputItemType="Analyzer" 
                       ReferenceOutputAssembly="false" />
     <AdditionalFiles Include="*.resx" />
   </ItemGroup>
   ```

3. Remove the Resources.tt files and Resources.cs files (they will be auto-generated during build)

4. Build the solution:
```bash
dotnet build
```

## Benefits

- Cross-platform compatibility - works on Windows, macOS, and Linux
- No Visual Studio dependency 
- Automatically runs as part of the regular build process
- No manual template execution required
- Much faster than T4 processing
