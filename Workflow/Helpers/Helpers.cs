﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;
using Workflow.Models;

namespace Workflow
{
    public class Helpers
    {
        private static UmbracoHelper _helper = new UmbracoHelper(UmbracoContext.Current);
        private static IUserService _us = ApplicationContext.Current.Services.UserService;
        private static Database _db = ApplicationContext.Current.DatabaseContext.Database;
        private static PocoRepository _pr = new PocoRepository();

        public static IPublishedContent GetNode(int id)
        {
            return _helper.TypedContent(id);
        }

        public static IUser GetUser(int id)
        {
            return _us.GetUserById(id);
        }

        public static IUser GetCurrentUser()
        {
            return UmbracoContext.Current.Security.CurrentUser;
        }

        public static bool IsTypeOfAdmin(string utAlias)
        {
            return utAlias == "admin" || utAlias == "siteadmin";
        }

        public static string PascalCaseToTitleCase(string str)
        {
            if (str != null)
            {
                return Regex.Replace(str, "([A-Z]+?(?=(([A-Z]?[a-z])|$))|[0-9]+)", " $1").Trim();
            }
            return null;
        }

        public static WorkflowSettingsPoco GetSettings()
        {
            return _pr.GetSettings();
        }

        /// <summary>Checks whether the email address is valid.</summary>
        /// <param name="email">the email address to check</param>
        /// <returns>true if valid, false otherwise.</returns>
        public static bool IsValidEmailAddress(string email)
        {
            try
            {
                MailAddress m = new MailAddress(email);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsNotFastTrack(WorkflowInstancePoco instance)
        {
            //var fasttrackDoctypes = GetSettings().FastTrack.Where(x => !string.IsNullOrWhiteSpace(x.ToString())).ToArray();
            //string nodeAlias = ApplicationContext.Current.Services.ContentService.GetById(instance.NodeId).ContentType.Alias.ToLower();
            //return (fasttrackDoctypes.IndexOf(nodeAlias) == -1);
            return true;
        }

        /// <summary>
        ///  Build a workflow instance history list markup for the workflow tab.
        /// </summary>
        /// <param name="instances">The workflow instances to include in the list.</param>
        /// <param name="includeAction">true if the Action link should be included for those who have access to it.</param>
        /// <param name="includeCancel">true if the Cancel link should be included for those who have access to it.</param>
        /// <param name="includeComments">true if comments should be included in the details</param>
        /// <returns>HTML table definition</returns>
        public static string BuildProcessList(List<WorkflowInstancePoco> instances, bool includeAction, bool includeCancel, bool includeComments)
        {
            string result = "";

            if (instances != null && instances.Count > 0)
            {
                result += "<table style=\"workflowProcessList\">";
                foreach (WorkflowInstancePoco instance in instances)
                {
                    result += "<tr>" + BuildProcessSummary(instance, includeAction, includeCancel, includeComments) + "</tr>";
                }
                result += "</table>";
            }
            else
            {
                result += "&nbsp;None.<br/><br/>";
            }

            return result;
        }

        /// <summary>
        /// Builds workflow instance details markup.
        /// </summary>
        /// <param name="instances">The workflow instances to include in the list.</param>
        /// <param name="includeAction">true if the Action link should be included for those who have access to it.</param>
        /// <param name="includeCancel">true if the Cancel link should be included for those who have access to it.</param>
        /// <param name="includeComments">true if comments should be included in the details</param>
        /// <returns>HTML tr inner html definition</returns>
        public static string BuildProcessSummary(WorkflowInstancePoco instance, bool includeAction, bool includeCancel, bool includeComments)
        {
            string result = "";

            result = instance.TypeDescription + " requested by " + instance.AuthorUser.Name + " on " + instance.CreatedDate.ToString("dd/MM/yy") + " - " + instance.Status + "<br/>";
            if (includeComments && !string.IsNullOrEmpty(instance.AuthorComment))
            {
                result += "&nbsp;&nbsp;Comment: <i>" + instance.AuthorComment + "</i>";
            }
            result += "<br/>";

            foreach (WorkflowTaskInstancePoco taskInstance in instance.TaskInstances)
            {
                if (taskInstance.Status == (int)TaskStatus.PendingApproval)
                {
                    result += BuildActiveTaskSummary(taskInstance, includeAction, includeCancel, false) + "<br/>";
                }
                else
                {
                    result += BuildInactiveTaskSummary(taskInstance, includeComments) + "<br/>";
                }
            }

            return result + "<br/>";
        }

        /// <summary>
        /// Creates a list of workflow task instances to be reviewed / actioned.
        /// </summary>
        /// <param name="taskInstances">Active task instances.</param>
        /// <param name="includeAction">true if the Action link should be included for those who have access to it.</param>
        /// <param name="includeCancel">true if the Cancel link should be included for those who have access to it.</param>
        /// <param name="includeEdit">true if the Edit icon should be included.</param>
        /// <returns>html markup describing a table of instance details </ul></returns>
        public static string BuildActiveTasksList(List<WorkflowTaskInstancePoco> taskInstances, bool includeAction, bool includeCancel, bool includeEdit)
        {
            string result = "";

            if (taskInstances != null && taskInstances.Count > 0)
            {
                result += "<table style=\"workflowTaskList\">";
                result += "<tr><th>Type</th><th>Page</th><th>Requested by</th><th>On</th><th>Approver</th><th>Comments</th><th>Activities</th></tr>";
                foreach (WorkflowTaskInstancePoco taskInstance in taskInstances)
                {
                    result += "<tr>" + BuildActiveTaskSummary(taskInstance, includeAction, includeCancel, includeEdit) + "</tr>";
                }
                result += "</table>";
            }
            else
            {
                result += "&nbsp;None.<br/><br/>";
            }

            return result;
        }

        /// <summary>
        /// Create html markup for an active workflow task including links to action, cancel, view, difference it.
        /// </summary>
        /// <param name="taskInstance">The task instance.</param>
        /// <param name="includeAction">true if the Action link should be included for those who have access to it.</param>
        /// <param name="includeCancel">true if the Cancel link should be included for those who have access to it.</param>
        /// <param name="includeEdit">true if the Edit icon should be included.</param>
        /// <returns>HTML markup describing an active task instance.</ul></returns>
        public static string BuildActiveTaskSummary(WorkflowTaskInstancePoco taskInstance, bool includeAction, bool includeCancel, bool includeEdit)
        {
            string result = "";

            // Get the node from the cache if it's already published, otherwise look up the document from the DB
            int docId = taskInstance.WorkflowInstance.NodeId;
            string docTitle = "";
            string docUrl = "";
            string pageViewLink = "";
            string pageEditLink = "";
            string differencesLink = "";
            umbraco.NodeFactory.Node n = new umbraco.NodeFactory.Node(docId);
            if (n.Id != 0)  // Published
            {
                docTitle = n.Name;
                if (taskInstance.WorkflowInstance.Type == (int)WorkflowType.Publish)
                {
                    differencesLink = "<a href=\"javascript:UmbClientMgr.openModalWindow('plugins/Usc/Dialogs/ShowDifferences.aspx?id=" +
                        taskInstance.WorkflowInstance.Id + "', 'Differences', true, 550, 520, 150, 150);\">Differences</a>";
                }
                else // Unpublish workflow doesnt need a differences link
                {
                    differencesLink = "";
                }
            }
            else // Unpublished
            {
                Document document = new Document(docId);
                docTitle = document.Text;
                differencesLink = "<a href=\"javascript:window.alert('This document has not previously been published.')\">Differences</a>";
            }
            docUrl = GetDocPreviewUrl(docId);

            if (includeEdit)
            {
                pageEditLink = "<img alt=\"Edit\" title=\"Edit this document\" style=\"float:right\" src=\"../../images/edit.png\" onClick=\"window.open('" + "');\">";
            }

            pageViewLink = "<a  target=\"_blank\" href=\"" + docUrl + "\">" + docTitle + "</a>";

            string createdDate = taskInstance.CreatedDate.ToString("dd/MM/yy");

            string authorText = taskInstance.WorkflowInstance.AuthorUser.Name;

            string approverText = "<a title='" + taskInstance.UserGroup.UsersSummary + "'>" + taskInstance.UserGroup.Name + "</a>";

            string cancelLink = "";
            if (includeCancel)
            {
                cancelLink = "<a href=\"javascript:UmbClientMgr.openModalWindow('plugins/Usc/Dialogs/CancelWorkflow.aspx?id=" +
                    taskInstance.WorkflowInstance.Id + "', 'Cancel workflow - " + taskInstance.WorkflowInstance.TypeDescription + "', true, 450, 240, 200, 100, '', backoffice.util.handleDialogClose);\">Cancel</a>";
            }

            string actionLink = "";
            if (includeAction && taskInstance.UserGroup.IsMember(Helpers.GetCurrentUser().Id)) // show the action link only if the current user is able to actually action the workflow.
            {
                actionLink = "<a href=\"javascript:UmbClientMgr.openModalWindow('plugins/Usc/Dialogs/ActionWorkflow.aspx?id=" +
                    taskInstance.WorkflowInstance.Id + "', 'Action workflow - " + taskInstance.WorkflowInstance.TypeDescription + "', true, 450, 350, 200, 100, '', backoffice.util.handleDialogClose);\">Action</a>";
            }

            result += "<td>" + taskInstance.WorkflowInstance.TypeDescription + "</td><td><div>" + pageViewLink + "&nbsp" + pageEditLink + "</div></td><td>" + authorText + "</td><td>" + createdDate + "</td><td>" + approverText +
                "</td><td><small>" + taskInstance.WorkflowInstance.AuthorComment + "</small></td><td>" + actionLink + " " + differencesLink + " " + cancelLink + "</td>";

            return result;
        }

        /// <summary>
        /// Create simple html markup for an inactive workflow task.
        /// </summary>
        /// <param name="taskInstances">The task instance.</param>
        /// <param name="includeComments">true if the comments should be included..</param>
        /// <returns>HTML markup describing an active task instance.</ul></returns>
        public static string BuildInactiveTaskSummary(WorkflowTaskInstancePoco taskInstance, bool includeComments)
        {
            string result = taskInstance.TypeName;

            if (taskInstance.Status == (int)TaskStatus.Approved 
                || taskInstance.Status == (int)TaskStatus.Rejected 
                || taskInstance.Status == (int)TaskStatus.Cancelled)
            {
                result += ": " + taskInstance.Status + " by " + taskInstance.ActionedByUser.Name + " on " + taskInstance.CompletedDate.Value.ToString("dd/MM/yy");
                if (includeComments && !string.IsNullOrEmpty(taskInstance.Comment))
                {
                    result += "<br/>&nbsp;&nbsp;Comment: <i>" + taskInstance.Comment + "</i>";
                }
            }
            else if (taskInstance.Status == (int)TaskStatus.NotRequired)
            {
                result += ": Not Required";
            }

            return result;
        }

        public static string GetUrlPrefix()
        {
            if (HttpContext.Current != null)
            {
                string absUri = HttpContext.Current.Request.Url.AbsoluteUri.ToLower();
                return absUri.Substring(0, absUri.IndexOf("/umbraco"));
            }
            else
            {
                return "";  // TODO There is no easy way to manage this as the out of context thread is managed by umbraco... :(
            }
        }

        public static string GetDocPreviewUrl(int docId)
        {
            return GetUrlPrefix() + "/umbraco/dialogs/preview.aspx?id=" + docId;
        }

        public static string GetDocPublishedUrl(int docId)
        {
            return GetUrlPrefix() + umbraco.library.NiceUrl(docId);
        }

        public static bool IUserCanAdminWorkflow(IUser user)
        {
            return Helpers.IsTypeOfAdmin(user.UserType.Alias);
        }

        public static string BuildEmailSubject(EmailType emailType, WorkflowInstancePoco instance)
        {
            return WorkflowInstancePoco.EmailTypeName(emailType) + " - " + instance.Node.Name + " (" + instance.TypeDescription + ")";

        }

        /// <summary>
        /// This method needed as the Document.VersionDate field is the last save date only for unpublished docs. For published docs its the last publish date!? 
        /// </summary>
        /// <param name="docId">The document to get the last update date for</param>
        /// <returns>The true last update date for a document</returns>
        public static DateTime GetDocumentLastEditDate(int nodeId)
        {
            DateTime result = new DateTime();

            //// The date from the doc above is incorrect! Return the actual date...
            //using (IRecordsReader dr =
            //    Application.SqlHelper.ExecuteReader("select updateDate from cmsDocument where nodeId = @nodeId order by updateDate desc",
            //                            Application.SqlHelper.CreateParameter("@nodeId", nodeId)))
            //{
            //    while (dr.Read())
            //    {
            //        result = dr.GetDateTime("updateDate");
            //        break;
            //    }
            //}
            return result;
        }
    }
}