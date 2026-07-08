# Breadmore Installer

Public bootstrap repository for Breadmore. This repo intentionally contains only the tiny installer package, not `com.breadmore.core` runtime/editor implementation code.

## Unity Install URL

Open **Window > Package Manager > + > Install package from git URL** and install:

```
https://github.com/breadmore/BreadmoreInstaller.git?path=Packages/com.breadmore.installer
```

Then open **Breadmore > Installer**, save a GitHub PAT with read access to the private `breadmore/BreadmoreCore` repo, and click **Install Breadmore Core**. The private Core package provides the full Breadmore menu and package manager.

## Public Contents

```
Packages/com.breadmore.installer
```
