require 'rake'
require 'build_utils.rb'

# project
PROJECT_NAME = "DbSnapshot"
COPYRIGHT = "Copyright © 2009 Dane O'Connor. All rights reserved."
MAJOR_VERSION = 0
MINOR_VERSION = 1

# compilation
COMPILE_MODE = "release"
CLR_VERSION = "v3.5"

# paths
SOURCE_DIR = "src"
OUTPUT_DIR = "output"
WORK_DIR = "temp"
PACKAGE_DIR = "output/pkg"
BIN_DIR = "output/bin"
NUNIT_EXE = "tools/NUnit/nunit-console.exe"

task :main => [:init, :clean, :compile, :unit_test, :publish]
task :init => [:output]

desc "Creates output directory"
directory OUTPUT_DIR

desc "Creates temporary work directory"
directory WORK_DIR

desc "Prepares the working directory for a new build"
task :clean do
	MSBuildRunner.clean :solutionfile => SOLUTION_FILE
	
	clean = Rake::FileList.new
	clean.include(File.join(SOURCE_DIR, '**/*.suo'))
	clean.include(File.join(SOURCE_DIR, '**/*.sln.cache'))
	clean.include(File.join(SOURCE_DIR, '**/*.csproj.user'))
	clean.include(File.join(SOURCE_DIR, '**/obj'))
	clean.include(WORK_DIR)
	
	puts "Cleaning directories..." 
	
	clean.each { |fn| rm_r fn rescue nil }
	
	puts "Clean succeeded.".green
end

desc "Restores working directory to fresh checkout state"
task :clobber => [:clean] do
	clobber = Rake::FileList.new
	clobber.include OUTPUT_DIR
	clobber.each { |fn| rm_r fn rescue nil }
end

desc "Compiles the solution"
task :compile => [:clean, :init, :copy_source_to_work_dir, :update_assembly_info] do
	solutions = FileList[File.join(WORK_DIR, *.sln)]
	
	solutions.each do |solution_path|
		MSBuildRunner.compile :compilemode => COMPILE_MODE,
			:solutionfile => solution_path,
			:clrversion   => CLR_VERSION
	end
end

desc "Runs unit tests"
task :unit_test => :compile do
	runner = NUnitRunner.new :compilemode => COMPILE_MODE,
		:source       => 'src',
		:tools        => 'tools',
		:results_file => File.join(output_dir, "nunit.xml")
	runner.executeTests Dir.glob(File.join(WORK_DIR, '*Test*.csproj').collect { |proj| proj.split('/').last }
end

desc "Runs tests with NUnit only (without coverage)."
task :test => [:compile] do
	puts "Running Tests..."
	
	assemblies_to_test = FileList["#{WORK_DIR}/**/#{COMPILE_MODE}/*.Test.dll"].exclude(/obj\//)
	
	runner = NUnitRunner.new(:tool => NUNIT_EXE, :exclude_categories => ['Performance'])
	runner.test(assemblies_to_test)
	
	puts "Tests Successful".green
end

desc "Update AssemblyInfo.cs(s) with current project info."
task :update_assembly_info => [:init] do
	
	injectables = Hash.new()
	injectables['<major_version>']  = MAJOR_VERSION
 	injectables['<minor_version>']  = MINOR_VERSION
 	injectables['<copyright>']      = COPYRIGHT
	
	puts "Updating AssemblyInfo Files..."
	infos = FileList["#{WORK_DIR}/**/AssemblyInfo.cs"]
	
	infos.each do |path|
		puts "AssemblyInfo file found at: #{path}"
		File.open(path, 'r+') do |f|
			text = f.read
			
			injectables_in_file = injectables.select { |k, v| text.include? k }
			
			unless injectables_in_file.empty? 
				injectables_in_file.each { |k, v| text.gsub!(k, v) }
				f.rewind
				f.write text
				injectables_in_file.each { |k, v| puts "replaced #{k} with #{v} in file @ #{path}"}
			end
		end
	end
end

desc "Copies the source to a temporary work directory. This way its safe to perform one time injections of information." 
task :copy_source_to_work_dir => :clean do
	cp_r SOURCE_DIR WORK_DIR
end

desc "Publishes the main library's compiled binaries to #{BIN_DIR}"
task :publish => :unit_test do
	cp_r File.join(WORK_DIR, '/DbSnapshot/bin/Release/') BIN_DIR
end

Rake::PackageTask.new(PROJECT_NAME, VERSION) do |p|
	p.need_zip = true
	p.package_files.include("output/*")
	p.package_dir = PACKAGE_DIR
end


