source('./r_files/flatten_HTML.r')

############### Library Declarations ###############
libraryRequireInstall("DT");
libraryRequireInstall("reshape2");
libraryRequireInstall("dplyr");
libraryRequireInstall("knitr");

################### Actual code #################### 
f <- melt(Values, id=c("Frequency"), measure.vars=c("Frequency"))
f <- f[order(f$Frequency),]
f <- f %>% distinct
f <- f %>% mutate(id = row_number())
f <- select(f,Frequency)
f <- datatable(f, width='150px', options = list(pageLength = 5, autoWidth = T) )
		
############# Create and save widget ###############
internalSaveWidget(f, 'out.html');
####################################################
