using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;

namespace knoxxr.Evelvator.Core
{
    public static class Logger
    {
        // 1. ILog 필드를 null로 선언만 합니다. (초기화하지 않음)
        private static readonly ILog log;

        static Logger()
        {
            // 2. log4net 설정 파일 경로 지정
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

            // 3. log4net.config 파일을 로드하고 설정 초기화
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            // 4. ***설정 완료 후*** ILog 인스턴스를 가져와 필드를 초기화합니다.
            //    이것이 순서를 보장하는 가장 확실한 방법입니다.
            log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        }

        public static void Debug(string text)
        {
            log.Debug(text);
        }
        public static void Info(string text)
        {
            log.Info(text);
        }
        public static void Warn(string text)
        {
            log.Warn(text);
        }
        public static void Error(string text)
        {
            log.Error(text);
        }
    }
}