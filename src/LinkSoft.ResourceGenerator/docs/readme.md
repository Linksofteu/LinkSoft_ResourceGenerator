# Resource Generator for .NET Projects

## Overview

This project replaces the T4 templates used for resource generation with cross-platform Roslyn Source Generator.

## Installation Steps

1. For each project containing a Resources.tt file (BackOffice.Resources, CustomerPortal.Resources, LeadManagement.Resources), update the project file:

   a. Install the NuGet package:
   ```bash
   dotnet add package LinkSoft.ResourceGenerator
   ```

   b. Remove the T4 template related items:
   ```xml
   <ItemGroup>
     <None Update="Resources.tt">
       <Generator>TextTemplatingFileGenerator</Generator>
       <LastGenOutput>Resources.cs</LastGenOutput>
     </None>
   </ItemGroup>

   <ItemGroup>
    <Service Include="{some-weird-guid}" />
  </ItemGroup>

   <ItemGroup>
     <Compile Update="Resources.cs">
       <DesignTime>True</DesignTime>
       <AutoGen>True</AutoGen>
       <DependentUpon>Resources.tt</DependentUpon>
     </Compile>
   </ItemGroup>
   ```

   c. Make sure that the Resources.cs and Resources.tt files are deleted and excluded from the project
  <ItemGroup>
    <!-- Exclude the temporary Resources.cs file from compilation since we're using the generated one -->
    <Compile Remove="Resources.cs" />
    <!-- Delete the Resources.tt file since we don't need it anymore -->
    <None Remove="Resources.tt" />
  </ItemGroup>

2. Remove the Resources.tt files and Resources.cs files (they will be auto-generated during build)

3. Build the solution:
```bash
dotnet build
```

## Benefits

- Cross-platform compatibility - works on Windows, macOS, and Linux
- No Visual Studio dependency 
- Automatically runs as part of the regular build process, no manual template execution required
- Much faster than T4 processing
- Full intellisense support