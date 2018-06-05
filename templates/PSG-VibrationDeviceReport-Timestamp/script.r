source('./r_files/flatten_HTML.r')

############### Library Declarations ###############
libraryRequireInstall("DT");
libraryRequireInstall("reshape2");
libraryRequireInstall("dplyr");
libraryRequireInstall("knitr");
####################################################

################### Actual code #################### 
data_z <- melt(Values, id=c("Timestamp"), measure.vars=c("Timestamp"))
data_z <- data_z %>% distinct
data_z <- data_z %>% mutate(id = row_number())
t1 <- select(data_z,Timestamp)
p1 <- datatable(t1, width='250px', options = list(pageLength = 5, autoWidth = T) )
		
############# Create and save widget ###############
internalSaveWidget(p1, 'out.html');
####################################################
