﻿/*
*
* 文件名    ：GroupController                          
* 程序说明  : 组相关数据控制器
* 更新时间  : 2020-05-21 11:43
* 联系作者  : QQ:779149549 
* 开发者群  : QQ群:874752819
* 邮件联系  : zhouhaogg789@outlook.com
* 视频教程  : https://space.bilibili.com/32497462
* 博客地址  : https://www.cnblogs.com/zh7791/
* 项目地址  : https://github.com/HenJigg/WPF-Xamarin-Blazor-Examples
* 项目说明  : 以上所有代码均属开源免费使用,禁止个人行为出售本项目源代码
*/

namespace Consumption.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Consumption.Core.Response;
    using Consumption.Core.Entity;
    using Consumption.Core.Query;
    using Consumption.Core.RequestForm;
    using Consumption.EFCore;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
    using Microsoft.Extensions.Logging;
    using NLog.Fluent;

    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class GroupController : Controller
    {
        private readonly ILogger<GroupController> logger;
        private readonly IUnitOfWork work;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="work"></param>
        public GroupController(ILogger<GroupController> logger, IUnitOfWork work)
        {
            this.logger = logger;
            this.work = work;
        }

        /// <summary>
        /// 获取组列表
        /// </summary>
        /// <param name="parameters">请求参数</param>
        /// <returns>结果</returns>
        [HttpGet]
        public async Task<IActionResult> GetGroups([FromQuery] QueryParameters parameters)
        {
            try
            {
                var models = await work.GetRepository<Group>().GetPagedListAsync(
                    predicate: x =>
                    string.IsNullOrWhiteSpace(parameters.Search) ? true : x.GroupCode.Contains(parameters.Search) ||
                    string.IsNullOrWhiteSpace(parameters.Search) ? true : x.GroupName.Contains(parameters.Search),
                    pageIndex: parameters.PageIndex,
                    pageSize: parameters.PageSize);

                return Ok(new ConsumptionResponse()
                {
                    success = true,
                    dynamicObj = models,
                    TotalRecord = models.TotalCount
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                return Ok(new ConsumptionResponse()
                {
                    success = false,
                    message = "获取组数据错误"
                });
            }
        }

        /// <summary>
        /// 保存组数据(新增/更新)
        /// </summary>
        /// <param name="model">组数据</param>
        /// <returns>结果</returns>
        [HttpPost]
        public async Task<IActionResult> SaveGroup([FromBody] GroupForm model)
        {
            try
            {
                if (model == null || model.group == null)
                    return Ok(new ConsumptionResponse() { success = false, message = "请求参数有误" });
                var newgroup = model.group;
                if (newgroup.Id > 0)
                {
                    //ID存在为更新,需要处理是否真实存在？
                    var group = await work.GetRepository<Group>()
                    .GetFirstOrDefaultAsync(predicate: x => x.Id == newgroup.Id);
                    if (group == null)
                        return Ok(new ConsumptionResponse() { success = false, message = "该用户组已不存在。" });

                    group.GroupName = newgroup.GroupName;
                    work.GetRepository<Group>().Update(group);
                    #region 更新组用户

                    //查询组下已存在的用户
                    var groupUsers = await work.GetRepository<GroupUser>().GetAllAsync(predicate: x => x.GroupCode == group.GroupCode);
                    for (int i = 0; i < groupUsers.Count; i++)
                    {
                        var arg = groupUsers[i];
                        //如果数据库存在的用户在更新列表当中查不到,代表删除,否则新增
                        var u = model.groupUser?.FirstOrDefault(t => t.Id == arg.Id);
                        if (u == null)
                            work.GetRepository<GroupUser>().Delete(arg);
                    }

                    #endregion

                    #region 更新组模块

                    //查询组下已存在的模块
                    var groupFuncs = await work.GetRepository<GroupFunc>().GetAllAsync(predicate: x => x.GroupCode == group.GroupCode);
                    for (int i = 0; i < groupFuncs.Count; i++)
                    {
                        var arg = groupFuncs[i];
                        var u = model.groupFunc?.FirstOrDefault(t => t.Id == arg.Id);
                        if (u == null)
                            work.GetRepository<GroupFunc>().Delete(arg);
                        else //如果存在,那么更新模块内容
                        {
                            arg.Auth = u.Auth;
                            work.GetRepository<GroupFunc>().Update(arg);
                            model.groupFunc.Remove(u);//移除
                        }
                    }

                    #endregion
                }
                else
                {
                    var group = await work.GetRepository<Group>()
                .GetFirstOrDefaultAsync(predicate: x => x.GroupCode == newgroup.GroupCode || x.GroupName == newgroup.GroupName);
                    if (group != null)
                        return Ok(new ConsumptionResponse() { success = false, message = "组编号/名称已重复,请勿重复添加!" });
                    //添加组信息
                    work.GetRepository<Group>().Insert(newgroup);
                }
                //添加新增组用户信息
                model.groupUser?.ForEach(u =>
                {
                    u.GroupCode = newgroup.GroupCode;
                    work.GetRepository<GroupUser>().Insert(u);
                });
                //添加新增组模块信息
                model.groupFunc?.ForEach(f =>
                {
                    f.GroupCode = newgroup.GroupCode;
                    work.GetRepository<GroupFunc>().Insert(f);
                });
                if (await work.SaveChangesAsync() > 0)
                    return Ok(new ConsumptionResponse() { success = true });
                return Ok(new ConsumptionResponse() { success = false, message = "新增组数据错误" });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "");
                return Ok(new ConsumptionResponse() { success = false, message = "新增组异常" });
            }
        }

        /// <summary>
        /// 删除组
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>结果</returns>
        [HttpDelete]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            try
            {
                var gpWork = work.GetRepository<Group>();
                var group = await gpWork.GetFirstOrDefaultAsync(predicate: x => x.Id == id);
                if (group == null)
                    return Ok(new ConsumptionResponse() { success = false, message = "该组已被删除" });
                gpWork.Delete(group);
                var guWork = work.GetRepository<GroupUser>();
                var gfWork = work.GetRepository<GroupFunc>();
                //移除所有的组用户
                var groupUsers = await guWork.GetAllAsync(predicate: x => x.GroupCode == group.GroupCode);
                for (int i = 0; i < groupUsers.Count; i++)
                    guWork.Delete(groupUsers);
                //移除所有的组模块
                var groupFuncs = await gfWork.GetAllAsync(predicate: x => x.GroupCode == group.GroupCode);
                for (int i = 0; i < groupFuncs.Count; i++)
                    gfWork.Delete(groupFuncs);

                if (await work.SaveChangesAsync() > 0)
                    return Ok(new ConsumptionResponse() { success = true });
                return Ok(new ConsumptionResponse() { success = false, message = "删除组数据失败" });
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return Ok(new ConsumptionResponse() { success = false, message = "删除组数据失败" });
            }
        }

        /// <summary>
        /// 获取菜单模块列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetMenuModules()
        {

            try
            {
                var menuItmes = await work.GetRepository<Menu>().GetAllAsync();
                var authItems = await work.GetRepository<AuthItem>().GetAllAsync();

                if (menuItmes.Count > 0)
                {
                    List<MenuModuleGroup> menuGroups = new List<MenuModuleGroup>();
                    for (int i = 0; i < menuItmes.Count; i++)
                    {
                        var m = menuItmes[i];
                        MenuModuleGroup group = new MenuModuleGroup();
                        group.MenuCode = m.MenuCode;
                        group.MenuName = m.MenuName;
                        for (int j = 0; j < authItems.Count; j++)
                        {
                            var au = authItems[j];
                            if ((m.MenuAuth & au.AuthValue) == au.AuthValue)
                            {
                                group.Modules.Add(new MenuModule() { Name = au.AuthName, Value = au.AuthValue });
                            }
                        }
                        menuGroups.Add(group);
                    }
                    return Ok(new ConsumptionResponse() { success = true, dynamicObj = menuGroups });
                }
                return Ok(new ConsumptionResponse() { success = true, });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                return Ok(new ConsumptionResponse()
                {
                    success = false,
                    message = "获取菜单模块列表错误"
                });
            }
        }
    }
}
