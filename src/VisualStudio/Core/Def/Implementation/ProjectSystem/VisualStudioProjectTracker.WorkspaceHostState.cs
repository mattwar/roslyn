// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
{
    internal sealed partial class VisualStudioProjectTracker
    {
        internal sealed class WorkspaceHostState
        {
            private readonly IVisualStudioWorkspaceHost _workspaceHost;
            private readonly VisualStudioProjectTracker _tracker;
            private readonly HashSet<AbstractProject> _pushedProjects;

            /// <summary>
            /// Set to true if we've already called <see cref="IVisualStudioWorkspaceHost.OnSolutionAdded(Microsoft.CodeAnalysis.SolutionInfo)"/>
            /// for this host. Set to false after the solution has closed.
            /// </summary>
            private bool _solutionAdded;

            public WorkspaceHostState(VisualStudioProjectTracker tracker, IVisualStudioWorkspaceHost workspaceHost)
            {
                _tracker = tracker;
                _workspaceHost = workspaceHost;
                _pushedProjects = new HashSet<AbstractProject>();

                this.HostReadyForEvents = false;
                _solutionAdded = false;
            }

            public bool Matches(IVisualStudioWorkspaceHost host)
            {
                return _workspaceHost == host;
            }

            /// <summary>
            /// Whether or not the project tracker has been notified that it should start to push state
            /// to the <see cref="IVisualStudioWorkspaceHost"/> or not.
            /// </summary>
            public bool HostReadyForEvents { get; set; }

            internal void SolutionClosed()
            {
                _solutionAdded = false;
                _pushedProjects.Clear();
            }

            internal void StartPushingToWorkspaceAndNotifyOfOpenDocuments(IEnumerable<AbstractProject> projects)
            {
                // If the workspace host isn't actually ready yet, we shouldn't do anything.
                // Also, if the solution is closing we shouldn't do anything either, because all of our state is
                // in the process of going away. This can happen if we receive notification that a document has
                // opened in the middle of the solution close operation.
                if (!this.HostReadyForEvents || _tracker._solutionIsClosing)
                {
                    return;
                }

#if true
                // We need to push these projects and any project dependencies we already know about. Therefore, compute the
                // transitive closure of the projects that haven't already been pushed, keeping them in appropriate order.
                var visited = new HashSet<AbstractProject>();
                var inOrderToPush = new List<AbstractProject>();

                foreach (var project in projects)
                {
                    AddToPushListIfNeeded(project, inOrderToPush, visited);
                }

                //var projectInfos = inOrderToPush.Select(p => p.CreateProjectInfoForCurrentState()).ToImmutableArray();
#endif

                if (!_solutionAdded)
                {
                    string solutionFilePath = null;
                    VersionStamp? version = default(VersionStamp?);

                    // Figure out the solution version
                    string solutionDirectory;
                    string solutionFileName;
                    string userOptsFile;
                    if (ErrorHandler.Succeeded(_tracker._vsSolution.GetSolutionInfo(out solutionDirectory, out solutionFileName, out userOptsFile)) && solutionFileName != null)
                    {
                        solutionFilePath = Path.Combine(solutionDirectory, solutionFileName);
                        if (File.Exists(solutionFilePath))
                        {
                            version = VersionStamp.Create(File.GetLastWriteTimeUtc(solutionFilePath));
                        }
                    }

                    if (version == null)
                    {
                        version = VersionStamp.Create();
                    }

                    var id = SolutionId.CreateNewId(string.IsNullOrWhiteSpace(solutionFileName) ? null : solutionFileName);
                    _tracker.RegisterSolutionProperties(id);

                    var solutionInfo = SolutionInfo.Create(id, version.Value, solutionFilePath /*, projects: projectInfos*/);

                    _workspaceHost.OnSolutionAdded(solutionInfo);

                    _solutionAdded = true;
                }

#if false
                else
                {
                    // The solution is already added, so we'll just do project added notifications from here
                    foreach (var projectInfo in projectInfos)
                    {
                        _workspaceHost.OnProjectAdded(projectInfo);
                    }
                }
#endif

                foreach (var project in inOrderToPush)
                {
                    // only push projects with documents alread open
                    if (HasOpenDocuments(project))
                    {
                        PushProjectAndDirectDependencies(project.Id);
                    }

                    project.StartPushingToWorkspaceHosts();
                }
            }

#if true
            private void AddToPushListIfNeeded(AbstractProject project, List<AbstractProject> inOrderToPush, HashSet<AbstractProject> visited)
            {
                if (_pushedProjects.Contains(project))
                {
                    return;
                }

                if (!visited.Add(project))
                {
                    return;
                }

                foreach (var projectReference in project.GetCurrentProjectReferences())
                {
                    AddToPushListIfNeeded(_tracker._projectMap[projectReference.ProjectId], inOrderToPush, visited);
                }

                inOrderToPush.Add(project);
            }
#endif

            private void PushProjectAndDirectDependencies(ProjectId projectId, DocumentId ignoreOpenDocId = null)
            {
                AbstractProject project;
                if (!_tracker._projectMap.TryGetValue(projectId, out project))
                    return;

                PushProjectAndDirectDependencies(project, ignoreOpenDocId);
            }

            private void PushProjectAndDirectDependencies(AbstractProject project, DocumentId ignoreOpenDocId = null)
            { 
                PushProject(project, ignoreOpenDocId);

                foreach (var pr in project.ProjectReferences)
                {
                    AbstractProject referencedProject;
                    if (_tracker._projectMap.TryGetValue(pr.ProjectId, out referencedProject))
                    {
                        if (HasOpenDocuments(referencedProject))
                        {
                            PushProjectAndDirectDependencies(referencedProject);
                        }
                        else                        
                        {
                            PushProject(referencedProject);
                        }
                    }
                }
            }

            private void PushProject(AbstractProject project, DocumentId ignoreOpenDocId = null)
            { 
                if (_pushedProjects.Contains(project))
                    return;

                // tell host to add project
                _workspaceHost.OnProjectAdded(project.CreateProjectInfoForCurrentState());

                project.StartPushingToWorkspaceHosts();
                _pushedProjects.Add(project);

                // notify host of all documents already open
                foreach (var document in project.GetCurrentDocuments())
                {
                    if (document.IsOpen && (ignoreOpenDocId == null || ignoreOpenDocId != document.Id))
                    {
                        _workspaceHost.OnDocumentOpened(
                            document.Id,
                            document.GetOpenTextBuffer(),
                            isCurrentContext: document.Project.Hierarchy == LinkedFileUtilities.GetContextHierarchy(document, _tracker._runningDocumentTable));
                    }
                }
            }

            private bool HasOpenDocuments(AbstractProject project)
            {
                return project.GetCurrentDocuments().Any(d => d.IsOpen);
            }

            private IEnumerable<AbstractProject> GetParentProjects(AbstractProject project)
            {
                return _tracker._projectMap.Values.Where(proj => proj.ProjectReferences.Any(pr => pr.ProjectId == project.Id));
            }

            private IEnumerable<AbstractProject> GetChildProjects(AbstractProject project)
            {
                foreach (var pr in project.ProjectReferences)
                {
                    AbstractProject referencedProject;
                    if (_tracker._projectMap.TryGetValue(pr.ProjectId, out referencedProject))
                    {
                        yield return referencedProject;
                    }
                }
            }

            private bool ParentsHaveOpenDocuments(AbstractProject project)
            {
                return GetParentProjects(project).Any(HasOpenDocuments);
            }

            private bool HasPushed(ProjectId projectId)
            {
                AbstractProject project;
                return _tracker._projectMap.TryGetValue(projectId, out project)
                    && HasPushed(project);
            }

            private bool HasPushed(AbstractProject project)
            {
                return _pushedProjects.Contains(project);
            }

            private void RemoveUnnecessaryProjects()
            {
                var projects = _tracker._projectMap.Values.Where(p => !HasOpenDocuments(p) && !ParentsHaveOpenDocuments(p)).ToList();

                foreach (var project in projects)
                {
                    if (HasPushed(project))
                    {
                        _workspaceHost.OnProjectRemoved(project.Id);
                        _pushedProjects.Remove(project);
                    }
                }
            }

            public void OnOptionsChanged(ProjectId projectId, CompilationOptions compilationOptions, ParseOptions parseOptions)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnOptionsChanged(projectId, compilationOptions, parseOptions);
                }
            }

            public void OnMetadataReferenceAdded(ProjectId projectId, PortableExecutableReference metadataReference)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnMetadataReferenceAdded(projectId, metadataReference);
                }
            }

            public void OnMetadataReferenceRemoved(ProjectId projectId, PortableExecutableReference metadataReference)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnMetadataReferenceRemoved(projectId, metadataReference);
                }
            }

            public void OnProjectReferenceAdded(ProjectId projectId, ProjectReference projectReference)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnProjectReferenceAdded(projectId, projectReference);
                }
            }

            public void OnProjectReferenceRemoved(ProjectId projectId, ProjectReference projectReference)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnProjectReferenceRemoved(projectId, projectReference);
                }
            }
            
            public void OnDocumentOpened(DocumentId documentId, Text.ITextBuffer buffer, bool isCurrentContext)
            {
                PushProjectAndDirectDependencies(documentId.ProjectId, ignoreOpenDocId: documentId);
                _workspaceHost.OnDocumentOpened(documentId, buffer, isCurrentContext);
            }

            public void OnDocumentClosed(DocumentId documentId, Text.ITextBuffer buffer, TextLoader loader, bool updateActiveContext)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnDocumentClosed(documentId, buffer, loader, updateActiveContext);
                    RemoveUnnecessaryProjects();
                }
            }

            public void OnDocumentTextUpdatedOnDisk(DocumentId documentId)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnDocumentTextUpdatedOnDisk(documentId);
                }
            }

            public void OnAdditionalDocumentOpened(DocumentId documentId, Text.ITextBuffer buffer, bool isCurrentContext)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnAdditionalDocumentOpened(documentId, buffer, isCurrentContext);
                }
            }

            public void OnAdditionalDocumentClosed(DocumentId documentId, Text.ITextBuffer buffer, TextLoader loader)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnAdditionalDocumentClosed(documentId, buffer, loader);
                }
            }

            public void OnAdditionalDocumentTextUpdatedOnDisk(DocumentId documentId)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnAdditionalDocumentTextUpdatedOnDisk(documentId);
                }
            }

            public void OnDocumentAdded(DocumentInfo documentInfo)
            {
                if (HasPushed(documentInfo.Id.ProjectId))
                {
                    _workspaceHost.OnDocumentAdded(documentInfo);
                }
            }

            public void OnDocumentRemoved(DocumentId documentId)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnDocumentRemoved(documentId);
                }
            }

            public void OnAdditionalDocumentAdded(DocumentInfo documentInfo)
            {
                if (HasPushed(documentInfo.Id.ProjectId))
                {
                    _workspaceHost.OnAdditionalDocumentAdded(documentInfo);
                }
            }

            public void OnAdditionalDocumentRemoved(DocumentId documentId)
            {
                if (HasPushed(documentId.ProjectId))
                {
                    _workspaceHost.OnAdditionalDocumentRemoved(documentId);
                }
            }

            public void OnOutputFilePathChanged(ProjectId projectId, string outputFilePath)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnOutputFilePathChanged(projectId, outputFilePath);
                }
            }

            public void OnAssemblyNameChanged(ProjectId projectId, string assemblyName)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnAssemblyNameChanged(projectId, assemblyName);
                }
            }

            public void OnProjectNameChanged(ProjectId projectId, string name, string projectFilePath)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnProjectNameChanged(projectId, name, projectFilePath);
                }
            }

            public void OnAnalyzerReferenceAdded(ProjectId projectId, AnalyzerReference analyzerReference)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnAnalyzerReferenceAdded(projectId, analyzerReference);
                }
            }

            public void OnAnalyzerReferenceRemoved(ProjectId projectId, AnalyzerReference analyzerReference)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnAnalyzerReferenceRemoved(projectId, analyzerReference);
                }
            }

            public void OnProjectRemoved(ProjectId projectId)
            {
                if (HasPushed(projectId))
                {
                    _workspaceHost.OnProjectRemoved(projectId);
                }
            }

            public void OnSolutionRemoved()
            {
                _workspaceHost.OnSolutionRemoved();
            }

            public void ClearSolution()
            {
                _workspaceHost.ClearSolution();
            }

            public void OnAfterWorkingFolderChange()
            {
                (_workspaceHost as IVisualStudioWorkingFolder)?.OnAfterWorkingFolderChange();
            }

            public void OnBeforeWorkingFolderChange()
            {
                (_workspaceHost as IVisualStudioWorkingFolder)?.OnBeforeWorkingFolderChange();
            }

            public void OnHasAllInformation(ProjectId projectId, bool hasAllInformation)
            {
                if (HasPushed(projectId))
                {
                    (_workspaceHost as IVisualStudioWorkspaceHost2)?.OnHasAllInformation(projectId, hasAllInformation);
                }
            }
        }
    }
}
