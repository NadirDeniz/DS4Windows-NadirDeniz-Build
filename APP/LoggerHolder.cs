/*
APP
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using APP;
using NLog;
using NLog.Targets.Wrappers;

namespace DS4WinWPF
{
    public class LoggerHolder
    {
        private Logger logger;// = LogManager.GetCurrentClassLogger();
        public Logger Logger { get => logger; }
        private ReaderWriterLockSlim logLock = new ReaderWriterLockSlim();

        public LoggerHolder(APP.ControlService service)
        {
            var configuration = LogManager.Configuration;
            var wrapTarget = configuration.FindTargetByName<WrapperTargetBase>("logfile") as WrapperTargetBase;
            var fileTarget = wrapTarget.WrappedTarget as NLog.Targets.FileTarget;
            fileTarget.FileName = $@"{APP.Global.appdatapath}\Logs\app_log.txt";
            fileTarget.ArchiveFileName = $@"{APP.Global.appdatapath}\Logs\app_log_{{#}}.txt";
            LogManager.Configuration = configuration;
            LogManager.ReconfigExistingLoggers();

            logger = LogManager.GetCurrentClassLogger();

            service.Debug += WriteToLog;
            APP.AppLogger.GuiLog += WriteToLog;
        }

        private void WriteToLog(object sender, APP.DebugEventArgs e)
        {
            if (e.Temporary)
            {
                return;
            }

            using WriteLocker locker = new WriteLocker(logLock);
            if (!e.Warning)
            {
                logger.Info(e.Data);
            }
            else
            {
                logger.Warn(e.Data);
            }
        }
    }
}
