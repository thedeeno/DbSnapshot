class NUnitRunner	
	def initialize(attributes)
		@tool = attributes[:tool]
		@results_file = attributes.fetch(:results_file, nil)
		@include_categories = attributes.fetch(:include_categories, nil)
		@exclude_categories = attributes.fetch(:exclude_categories, nil)
		@show_logo = attributes.fetch(:show_logo, false)
	end
	
	def test(assemblies_to_test)		
		cmd = @tool
		cmd << ' /xml:' + @results_file unless @results_file.nil?
		cmd << ' /nologo' unless @show_logo
		cmd << ' /include:' + @include_categories.join(',') unless @include_categories.nil? or @include_categories.length == 0
		cmd << ' /exclude:' + @exclude_categories.join(',') unless @exclude_categories.nil? or @exclude_categories.length == 0
		cmd	<< ' ' + assemblies_to_test.join(' ')
		
		sh cmd
	end
end

class TeamcityNUnitRunner
	def initialize(attributes)
		@tool = attributes.fetch(:tool, ENV["teamcity.dotnet.nunitlauncher"])
		@dot_net_framework = attributes.fetch(:dot_net_framework, 'v3.5')
		@platform = attributes.fetch(:platform, 'x86')
		@nunit_version = attributes.fetch(:nunit_version, 'NUnit-2.5.0')
		@include_categories = attributes.fetch(:include_categories, nil)
		@exclude_categories = attributes.fetch(:exclude_categories, nil)
		@addin = attributes.fetch(:addin, nil)
	end
	
	def test(assemblies_to_test)
		# ENV[teamcity.dotnet.nunitlauncher] <.NET Framework> <platform> <NUnit vers.> 
		# [/category-include:<list>] [/category-exclude:<list>] [/addin:<list>] <assemblies to test>
		# lists are seperated by ";"
		# sh "#{TEAMCITY_NUNIT_RUNNER} v3.5 x86 NUnit-2.5.0 /category-exclude:Performance #{tests}"
	
		cmd = @tool
		cmd << ' ' + @dot_net_framework
		cmd << ' ' + @platform
		cmd << ' ' + @nunit_version
		cmd << ' /category-include:' + @include_categories.join(';') if @include_categories.length > 0
		cmd << ' /category-exclude:' + @exclude_categories.join(';') if @exclude_categories.length > 0
		cmd << ' /addin:' + @addin.join(';') if @addin.length > 0
		cmd << ' ' + @assemblies_to_test.join(';')
		
		sh cmd
	end
end

class MSBuildRunner
	def self.compile(attributes)	
		solution_file  = attributes[:solutionfile]
		version        = attributes.fetch(:clrversion, 'v3.5')
		compile_mode   = attributes.fetch(:compilemode, 'release')
		msbuild_path   = attributes.fetch(:msbuild_path, File.join(ENV['windir'].dup, 'Microsoft.NET', 'Framework', version))		
		framework_dir  = File.join(ENV['windir'].dup, 'Microsoft.NET', 'Framework', version)
		msbuild_file   = File.join(framework_dir, 'msbuild.exe')
		sh "#{msbuild_file} #{solution_file} /maxcpucount /v:m /property:BuildInParallel=false /property:Configuration=#{compile_mode} /t:Rebuild"
	end
	
	def self.clean(attributes)
		solution_file  = attributes[:solutionfile]
		version        = attributes.fetch(:clrversion, 'v3.5')
		
		framework_dir  = File.join(ENV['windir'].dup, 'Microsoft.NET', 'Framework', version)
		msbuild_file   = File.join(framework_dir, 'msbuild.exe')
		compile_mode   = attributes.fetch(:compilemode, 'release')
		
		sh "#{msbuild_file} #{solution_file} /t:Clean /property:Configuration=#{compile_mode}"
	end
end

class SqlCmdRunner
	def initialize(*options)
		path    = options.fetch(:path, File.join(ENV['ProgramFiles'], 'Microsoft SQL Server', '90', 'Tools', 'Binn'))
		user    = options.fetch(:user, "")
		pass    = options.fetch(:pass, "")
		server  = options.fetch(:server, ".\\SQLEXPRESS")
		db_name = options.fetch(:db_name, "")
		
		tool = File.join(path, 'sqlcmd.exe')
		
		# create base command
		@cmd_base = "\"#{tool}\""
		@cmd_base << " -U #{user}" unless user.empty?
		@cmd_base << " -P #{pass}" unless pass.empty?
		@cmd_base << " -S #{server}" unless server.empty?
		@cmd_base << " -d #{db_name}" unless db_name.empty?
	end
	
	def execute_query query
		sh @cmd_base += " -Q #{query}"
	end
	
	def execute_file filename
		sh @cmd_base += " -i #{filename}"
	end
end


# Colorize console output
begin
  require 'Win32/Console/ANSI' if PLATFORM =~ /win32/
rescue LoadError
  raise 'You must gem install win32console to use color on Windows'
end

class String	
	# methods for colorizing console output
	# http://kpumuk.info/ruby-on-rails/colorizing-console-ruby-script-output/
	# format: ESC[<mode>;<forground>;<background><your_text>ESC[0m
	def red; colorize(self, "\e[1m\e[31m"); end
	def green; colorize(self, "\e[1m\e[32m"); end
	def dark_green; colorize(self, "\e[32m"); end
	def yellow; colorize(self, "\e[1m\e[33m"); end
	def blue; colorize(self, "\e[1m\e[34m"); end
	def light_blue; colorize(self, "\e[1;36m"); end
	def dark_blue; colorize(self, "\e[34m"); end
	def pur; colorize(self, "\e[1m\e[35m"); end
	def colorize(text, color_code)  "#{color_code}#{text}\e[0m" end
end