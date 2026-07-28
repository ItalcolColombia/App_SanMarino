using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// D1 del Informe RA Pesadas: extiende la guia genetica 2026 AP de Agroavicola
    /// Sanmarino (company_id = 1) de la semana 76 a la 97 con la curva de RECICLAJE
    /// (2do ciclo) del archivo "Informe RA Pesadas Parametros - Graficos 2025 v1".
    ///
    /// Origen del dato: las 5 guias *R del Excel (A289R, A299R, K291R, K307R, K309R)
    /// son LA MISMA curva de 28 semanas relativas, solo desplazada segun en que edad
    /// se reciclo cada lote (verificado alineando por semana relativa). Se toma esa
    /// curva unica y se ancla en rel 0 = semana 77, que es el UNICO anclaje posible
    /// sin pisar las semanas 1..76 del ciclo 1 ya cargadas.
    ///
    /// Lectura de las filas: 77..84 son la MUDA (sin produccion ni peso: solo
    /// consumo y retiro acumulandose) y 85..97 el 2do ciclo (repique 2% -> 65% en
    /// la semana 91 y declive). Los acumulados de consumo/retiro REINICIAN en la 85,
    /// tal como vienen en el archivo.
    ///
    /// LIMITACION CONOCIDA (aceptada al tomar D1): como el anclaje es fijo, un lote
    /// reciclado antes de la semana 77 se compara contra un punto de guia corrido
    /// hasta 6 semanas. La alternativa sin desfase (guardar la curva una vez y
    /// anclarla en la edad de reciclaje de cada lote) esta documentada en el
    /// plan (fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md, 4.1).
    ///
    /// Data-only (Designer clonado, ModelSnapshot intacto). Idempotente:
    /// INSERT ... WHERE NOT EXISTS por (company, anio, raza, edad).
    /// </summary>
    public partial class ExtenderGuia2026ApSemanas77a97 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202677', '2026', 'AP', '77', '0.3', '0.3', '1.2', '1.2', '310.48252297199053', '819', '44.35464613885579', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.91875626880642', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '77');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202678', '2026', 'AP', '78', '0.3', '0.5991', '1.2', '2.3855999999999997', '400.60841933336536', '1647.9473684210527', '12.741663720214175', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.838245931374868', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '78');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202679', '2026', 'AP', '79', '0.3', '0.8973027', '1.2', '3.5569728', '594.3315407336336', '2486.9629235030893', '27.502525688217133', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.75846236730027', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '79');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202680', '2026', 'AP', '80', '0.3', '1.1946107918999997', '1.2', '4.7142891264', '802.8435257272808', '3336.168950914058', '29.531946470334645', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.679399015940488', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '80');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202681', '2026', 'AP', '81', '0.3', '1.4910269595243', '1.2', '5.8577176568832', '1265.4800900404525', '4195.689221572933', '65.74582662889388', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.601049375876833', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '81');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202682', '2026', 'AP', '82', '0.3', '1.7865538786457271', '1.2', '6.987425045000603', '1897.5943619354096', '5065.649009689204', '89.75805829046435', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.523407004379449', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '82');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202683', '2026', 'AP', '83', '0.3', '2.0811942170097897', '1.2', '8.103575944460594', '2665.3435225500402', '5946.175111021462', '108.86274968433595', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.446465516877529', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '83');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202684', '2026', 'AP', '84', '0.3', '2.3749506343587607', '1.2', '9.206333033127068', '3498.7075579414127', '6837.395861357754', '117.90627779302879', '117', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '8.370218586434301', NULL, NULL, NULL, '0', NULL, NULL, NULL, NULL, '0', NULL, NULL, 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '84');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202685', '2026', 'AP', '85', '0.18', '0.18', '0.18', '0.18', '879.7425967378883', '922.3127133754685', '125.90414127399153', '132', '3800', '5200', NULL, '0.139748', '2', '0', '0', '65', '1.3', NULL, '0', '0', '0', '6847.627063699575', NULL, NULL, '7670.277978725128', '703.560732640788', '8.370218586434301', '118.55670103092784', 'F3', '2930', '2582.2939375295664', '12.5', '110.16612361474259', 'M', '2900', '2679.6', '11', '101.64', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '85');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202686', '2026', 'AP', '86', '0.18', '0.359676', '0.18', '0.359676', '1812.8991344338776', '1842.9652638668613', '133.7892847615718', '132', '3800', '5200', NULL, '0.6279855876', '7', '0.21970691441999995', '45', '67', '4.69', NULL, '70.7', '0.15533278849493998', '34.985980372521524', '3132.491245554804', '8953.561432896437', '12664.160442569219', '7656.471478363424', '702.2943233220346', '8.37', '118.71794871794872', 'F3', '2930', '2744.0182304598375', '12.5', '117.06562416637531', 'M', '2900', '2679.6', '11', '101.64', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '86');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202687', '2026', 'AP', '87', '0.18', '0.5390285832', '0.18', '0.5390285832', '2838.838120754675', '2761.9606397673697', '147.35700872793456', '132', '3900', '5200', NULL, '1.6723257874764001', '15', '1.0134054663260639', '76', '67.5', '10.125', NULL, '75.4', '0.7537814966321121', '60.59856721191444', '1835.7788216656365', '3029.4096149928423', '4072.8251851382684', '7642.689829702369', '701.0301935400549', '8.37', '118.87755102040816', 'F3', '2930', '3022.2922490099377', '12.5', '128.93738263694274', 'M', '2900', '2679.6', '11', '101.64', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '87');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202688', '2026', 'AP', '88', '0.18', '0.71805833175024', '0.18', '0.71805833175024', '3881.2985082712976', '3686.251378417195', '150', '133', '3900', '5200', NULL, '4.452220154187393', '40', '3.4690859286874107', '88.33718618117', '68', '27.2', NULL, '77', '2.6446554526503494', '77.91811295370633', '941.0688737729287', '1207.766510377437', '1584.2690593556897', '7628.932988008904', '699.7683391916828', '8.37', '119.18678526048285', 'F3', '2930', '3076.5', '12.5', '131.25', 'M', '2900', '2699.9', '11', '102.41000000000001', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '88');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202689', '2026', 'AP', '89', '0.18', '0.8967658267530897', '0.18', '0.8967658267530897', '4956.5685990510265', '4608.878393737452', '155', '133', '4000', '5200', NULL, '8.2676946698574', '55', '7.023971747373419', '93.17021524023365', '68', '37.4', NULL, '78.5', '5.435240820318866', '84.95683534349203', '646.1706688887602', '760.5870278432621', '982.9080203802963', '7615.200908630489', '698.5087561811378', '8.37', '119.49367088607595', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2699.9', '11', '102.41000000000001', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '89');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202690', '2026', 'AP', '90', '0.18', '1.075151648264934', '0.18', '1.075151648264934', '6029.903203667352', '5529.844680430131', '155', '133', '4000', '5200', NULL, '12.491785694476485', '61', '10.986887201656018', '93.81699947244766', '68', '41.48', NULL, '80', '8.605573183744943', '87.95289536958762', '519.762622391175', '590.9556703131558', '754.4835366892047', '7601.493546994954', '697.2514404200117', '8.37', '119.79823455233291', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2699.9', '11', '102.41000000000001', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '90');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202691', '2026', 'AP', '91', '0.18', '1.2532163752980572', '0.18', '1.2532163752980572', '7101.305805995368', '6456.0653221479415', '155', '134', '4000', '5200', NULL, '16.98476434940042', '65', '15.220779772036517', '94.23353404406895', '68', '44.2', NULL, '80', '11.992687240049342', '89.61431232676375', '449.91454861202675', '502.0565766007196', '637.1960205901854', '7587.8108586103635', '695.9963878272557', '8.37', '120.10050251256281', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2720.2', '11', '103.18', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '91');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202692', '2026', 'AP', '92', '0.18', '1.4309605858225205', '0.18', '1.4309605858225205', '8170.779883639193', '7380.618766710661', '155', '134', '4000', '5200', NULL, '21.435156478950535', '64.5', '19.43738881671969', '94.74691042808008', '69', '44.505', NULL, '80', '15.36597447579588', '90.67994831671666', '410.00651504805603', '452.1468336263621', '571.9490046848503', '7574.152799064865', '694.7435943291666', '8.37', '120.40050062578223', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2720.2', '11', '103.18', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '92');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202693', '2026', 'AP', '93', '0.18', '1.60838485676804', '0.18', '1.60838485676804', '9238.328907943258', '8303.508015073165', '155', '134', '4200', '5200', NULL, '25.80866377206719', '63.5', '23.60598206608391', '95.31465183389669', '69', '43.815', NULL, '80', '18.70084907528726', '91.46533999033592', '384.8843460814068', '420.79802701446596', '531.1711056089786', '7560.519324026548', '693.4930558593742', '8.37', '120.69825436408979', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2720.2', '11', '103.18', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '93');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202694', '2026', 'AP', '94', '0.18', '1.7854897640258578', '0.18', '1.7854897640258578', '10303.956344003576', '9231.610898965599', '155', '135', '4200', '5200', NULL, '30.10554859489106', '62.5', '27.714763471213075', '95.62233046844658', '69', '43.125', NULL, '80', '21.98787419939059', '92.0586561771417', '367.9276037898148', '399.66649424236516', '503.76231257513246', '7546.9103892433', '692.2447683588273', '8.37', '120.99378881987577', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2740.5', '11', '103.95', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '94');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202695', '2026', 'AP', '95', '0.18', '1.962275882450611', '0.18', '1.962275882450611', '11367.665650678988', '10158.043197667028', '155', '135', '4200', '5200', NULL, '34.326072618151564', '61.5', '31.761472533895795', '95.88167346945929', '69', '42.435', NULL, '79', '25.184774358909937', '92.52871100989394', '355.9369056385242', '384.6772550419128', '485.13105165485246', '7533.325950542662', '690.9987277757814', '8.37', '121.28712871287128', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2740.5', '11', '103.95', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '95');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202696', '2026', 'AP', '96', '0.18', '2.1387437858622', '0.18', '2.1387437858622', '12429.460280602383', '11082.807918230792', '155', '135', '4200', '5200', NULL, '38.470496818820294', '60.5', '35.74903315924765', '96.21506950732591', '70', '42.35', NULL, '78', '28.295071646684384', '92.92584217877513', '347.2041339049842', '373.6357139890285', '472.06508948487283', '7519.765963831685', '689.754930065785', '8.37', '121.57829839704068', 'F3', '2930', '3179.05', '12.5', '135.625', 'M', '2900', '2740.5', '11', '103.95', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '96');

INSERT INTO guia_genetica_sanmarino_colombia (company_id, codigo_guia_genetica, anio_guia, raza, edad, mort_sem_h, retiro_ac_h, mort_sem_m, retiro_ac_m, cons_ac_h, cons_ac_m, gr_ave_dia_h, gr_ave_dia_m, peso_h, peso_m, uniformidad, h_total_aa, prod_porcentaje, h_inc_aa, aprov_sem, peso_huevo, masa_huevo, grasa_porcentaje, nacim_porcentaje, pollito_aa, aprov_ac, gr_huevo_t, gr_huevo_inc, gr_pollito, hembras, machos, apareo, peso_mh, alim_h, kcal_h, kcal_sem_h, prot_h, prot_h_sem, alim_m, kcal_m, kcal_sem_m, prot_m, prot_sem_m, created_by_user_id, created_at)
SELECT 1, 'AP202697', '2026', 'AP', '97', '0.18', '2.314894047047648', '0.18', '2.314894047047648', '13482.50572277521', '12012.745841142481', '154', '136', '4200', '5200', NULL, '42.53908148176076', '59.5', '39.67086814319372', '96.39310248767593', '70', '41.65', NULL, '77', '31.314884584322854', '93.25746292900841', '340.5809035656674', '365.204985069058', '462.65534745623', '7506.230385096788', '688.5133711916666', '8.37', '121.86732186732188', 'F3', '2930', '3158.54', '12.5', '134.75', 'M', '2900', '2760.8', '11', '104.72', 1, now()
 WHERE NOT EXISTS (
   SELECT 1 FROM guia_genetica_sanmarino_colombia g
    WHERE g.company_id = 1 AND g.anio_guia = '2026' AND g.raza = 'AP' AND g.edad = '97');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM guia_genetica_sanmarino_colombia
 WHERE company_id = 1 AND anio_guia = '2026' AND raza = 'AP'
   AND NULLIF(regexp_replace(edad, '[^0-9]', '', 'g'), '')::int BETWEEN 77 AND 97;");
        }
    }
}
